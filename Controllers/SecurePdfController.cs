using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintingBooksPortal.Data;
using PrintingBooksPortal.Models;
using PrintingBooksPortal.Services;

namespace PrintingBooksPortal.Controllers;

[ApiController]
[Route("api/pdf")]
public class SecurePdfController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorageService _fileStorage;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PrintLoggingService _printLogging;
    private readonly IWatermarkService _watermarkService;
    private readonly PrintTokenService _printTokenService;
    private readonly IPdfSecurityService _pdfSecurity;
    private readonly ILogger<SecurePdfController> _logger;

    public SecurePdfController(
        AppDbContext db,
        FileStorageService fileStorage,
        UserManager<ApplicationUser> userManager,
        PrintLoggingService printLogging,
        IWatermarkService watermarkService,
        PrintTokenService printTokenService,
        IPdfSecurityService pdfSecurity,
        ILogger<SecurePdfController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _userManager = userManager;
        _printLogging = printLogging;
        _watermarkService = watermarkService;
        _printTokenService = printTokenService;
        _pdfSecurity = pdfSecurity;
        _logger = logger;
    }

    private async Task<(Book? book, ApplicationUser? user)> ValidateAccess(int bookId)
    {
        var book = await _db.Books.Include(b => b.Board).FirstOrDefaultAsync(b => b.Id == bookId && b.IsActive);
        if (book == null)
            return (null, null);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return (null, null);

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (isAdmin)
            return (book, user);

        var hasAccess = await _db.ShopBookAssignments
            .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == bookId && a.IsActive);

        return hasAccess ? (book, user) : (null, null);
    }

    private async Task<byte[]?> GetOriginalPdfBytes(int bookId)
    {
        var filePath = _fileStorage.GetFilePath((await _db.Books.FindAsync(bookId))?.FilePath ?? "");
        if (!System.IO.File.Exists(filePath))
            return null;
        return await System.IO.File.ReadAllBytesAsync(filePath);
    }

    [HttpGet("view-secure/{bookId}")]
    [Authorize(Roles = "Shop,Admin")]
    public async Task<IActionResult> ViewSecurePdf(int bookId)
    {
        var (book, user) = await ValidateAccess(bookId);
        if (book == null || user == null)
            return NotFound(new { error = "Access Denied: You are not authorized to view this book." });

        var shop = user.ShopId != null ? await _db.Shops.FindAsync(user.ShopId.Value) : null;
        var shopName = shop?.Name ?? "Unknown Shop";

        _logger.LogInformation("User {UserId} viewing secure PDF for book {BookId}", user.Id, bookId);

        try
        {
            var originalBytes = await System.IO.File.ReadAllBytesAsync(_fileStorage.GetFilePath(book.FilePath));
            var watermarked = _watermarkService.AddHeavyWatermark(originalBytes, shopName, user.UserName ?? "Unknown", DateTime.UtcNow);
            return Ok(new { pdfData = Convert.ToBase64String(watermarked) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Heavy watermarking failed for book {BookId}", bookId);
            return StatusCode(500, new { error = "Failed to process PDF for viewing." });
        }
    }

    [HttpPost("process-print")]
    [Authorize(Roles = "Shop")]
    public async Task<IActionResult> ProcessPrint([FromBody] ProcessPrintRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.ShopId == null)
            return Unauthorized(new { success = false, error = "Access Denied: You are not authorized to print." });

        var hasAccess = await _db.ShopBookAssignments
            .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == request.BookId && a.IsActive);

        if (!hasAccess)
            return Forbid();

        var book = await _db.Books.FindAsync(request.BookId);
        if (book == null)
            return NotFound(new { success = false, error = "Book not found." });

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { success = false, error = "PDF file not found on server." });

        var shop = await _db.Shops.FindAsync(user.ShopId.Value);
        var shopName = shop?.Name ?? "Unknown Shop";
        var copies = Math.Max(1, request.Copies);

        var jobId = Guid.NewGuid().ToString("N");
        var userPass = $"PRINT-{jobId}";
        var ownerPass = $"ADMIN-{jobId}";

        _logger.LogInformation("ProcessPrint: Job={JobId}, Book={BookId}, Shop={ShopId}, Copies={Copies}",
            jobId, request.BookId, user.ShopId, copies);

        try
        {
            var originalBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var watermarked = _watermarkService.AddHeavyWatermark(originalBytes, shopName, user.UserName ?? "Unknown", DateTime.UtcNow);
            var securedBytes = _pdfSecurity.EncryptPdfWithPassword(watermarked, userPass, ownerPass);

            var secureDir = Path.Combine(Directory.GetCurrentDirectory(), "SecurePrints");
            Directory.CreateDirectory(secureDir);
            var securePath = Path.Combine(secureDir, $"{jobId}.pdf");
            await System.IO.File.WriteAllBytesAsync(securePath, securedBytes);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _printLogging.LogPrintAsync(
                user.ShopId.Value,
                request.BookId,
                copies,
                user.Id,
                user.UserName
            );

            _logger.LogInformation("Print logged: Job={JobId}, Shop={ShopId}, Book={BookId}, Copies={Copies}, IP={IP}",
                jobId, user.ShopId, request.BookId, copies, ipAddress);

            return Ok(new
            {
                success = true,
                jobId,
                password = userPass,
                message = $"Print job {jobId} created for {copies} copy(ies). Password: {userPass}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessPrint failed for book {BookId}", request.BookId);
            return StatusCode(500, new { success = false, error = $"Failed to process print job: {ex.Message}" });
        }
    }

    [HttpGet("print-file/{jobId}")]
    public IActionResult GetPrintFile(string jobId)
    {
        var securePath = Path.Combine(Directory.GetCurrentDirectory(), "SecurePrints", $"{jobId}.pdf");
        if (!System.IO.File.Exists(securePath))
            return NotFound("Print job not found or expired.");

        var fileBytes = System.IO.File.ReadAllBytes(securePath);
        System.IO.File.Delete(securePath);

        return File(fileBytes, "application/pdf", $"print_{jobId}.pdf");
    }

    [HttpGet("print/{bookId}")]
    public async Task<IActionResult> PrintPdf(int bookId, [FromQuery] string? token = null)
    {
        Book? book = null;
        ApplicationUser? user = null;
        string shopName = "Unknown Shop";
        string userId = "unknown";
        string userName = "Unknown User";

        if (!string.IsNullOrEmpty(token))
        {
            if (_printTokenService.ValidateToken(token, out int tid, out userId, out shopName, out userName))
            {
                book = await _db.Books.Include(b => b.Board).FirstOrDefaultAsync(b => b.Id == tid && b.IsActive);
                if (book == null)
                    return NotFound();
            }
            else
            {
                return Unauthorized("Invalid or expired print token.");
            }
        }
        else
        {
            (book, user) = await ValidateAccess(bookId);
            if (book == null || user == null)
                return NotFound();

            var shop = user.ShopId != null ? await _db.Shops.FindAsync(user.ShopId.Value) : null;
            shopName = shop?.Name ?? "Unknown Shop";
            userId = user.Id;
            userName = user.UserName ?? "Unknown";
        }

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        _logger.LogInformation("Print request for book {BookId} by {UserName} (Shop: {ShopName})", bookId, userName, shopName);

        try
        {
            var originalBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var watermarked = _watermarkService.AddHeavyWatermark(originalBytes, shopName, userName, DateTime.UtcNow);
            return File(new MemoryStream(watermarked), "application/pdf", enableRangeProcessing: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Watermarking failed for book {BookId}, serving original PDF", bookId);
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/pdf", enableRangeProcessing: false);
        }
    }

    [HttpGet("print-token/{bookId}")]
    [Authorize(Roles = "Shop")]
    public async Task<IActionResult> GetPrintToken(int bookId)
    {
        var (book, user) = await ValidateAccess(bookId);
        if (book == null || user == null)
            return NotFound();

        var shop = user.ShopId != null ? await _db.Shops.FindAsync(user.ShopId.Value) : null;
        var shopName = shop?.Name ?? "Unknown Shop";

        var token = _printTokenService.GenerateToken(bookId, user.Id, shopName, user.UserName ?? "Unknown");
        return Ok(new { token, expiresInMinutes = 5 });
    }
}

public class ProcessPrintRequest
{
    public int BookId { get; set; }
    public int Copies { get; set; } = 1;
}
