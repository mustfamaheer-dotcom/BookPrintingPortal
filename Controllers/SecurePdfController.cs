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
    private readonly WatermarkService _watermarkService;
    private readonly PrintTokenService _printTokenService;
    private readonly PdfSecurityService _pdfSecurity;
    private readonly ILogger<SecurePdfController> _logger;

    public SecurePdfController(
        AppDbContext db,
        FileStorageService fileStorage,
        UserManager<ApplicationUser> userManager,
        PrintLoggingService printLogging,
        WatermarkService watermarkService,
        PrintTokenService printTokenService,
        PdfSecurityService pdfSecurity,
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

    [HttpGet("view-secure/{bookId}")]
    [Authorize(Roles = "Shop,Admin")]
    public async Task<IActionResult> ViewSecurePdf(int bookId)
    {
        var (book, user) = await ValidateAccess(bookId);
        if (book == null || user == null)
            return NotFound();

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        var shop = user.ShopId != null ? await _db.Shops.FindAsync(user.ShopId.Value) : null;
        var shopName = shop?.Name ?? "Unknown Shop";

        _logger.LogInformation("User {UserId} viewing secure PDF for book {BookId}", user.Id, bookId);

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var watermarked = _watermarkService.AddHeavyWatermark(fs, shopName, user.UserName ?? "Unknown", DateTime.UtcNow);
            var base64 = Convert.ToBase64String(watermarked);
            return Ok(new { pdfData = base64 });
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
            return Unauthorized();

        var hasAccess = await _db.ShopBookAssignments
            .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == request.BookId && a.IsActive);

        if (!hasAccess)
            return Forbid();

        var book = await _db.Books.FindAsync(request.BookId);
        if (book == null)
            return NotFound();

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        var shop = await _db.Shops.FindAsync(user.ShopId.Value);
        var shopName = shop?.Name ?? "Unknown Shop";
        var copies = Math.Max(1, request.Copies);

        var jobId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        _logger.LogInformation("ProcessPrint: Job={JobId}, Book={BookId}, Shop={ShopId}, Copies={Copies}",
            jobId, request.BookId, user.ShopId, copies);

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var watermarked = _watermarkService.AddHeavyWatermark(fs, shopName, user.UserName ?? "Unknown", DateTime.UtcNow);

            var securedFilePath = _pdfSecurity.CreateSecurePrintFile(watermarked, jobId, out var password);

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
                password,
                message = $"Print job {jobId} created for {copies} copy(ies). Password: {password}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessPrint failed for book {BookId}", request.BookId);
            return StatusCode(500, new { success = false, error = "Failed to process print job." });
        }
    }

    [HttpGet("print-file/{jobId}")]
    public IActionResult GetPrintFile(string jobId, [FromQuery] string? password = null)
    {
        var fileBytes = _pdfSecurity.GetSecurePrintFile(jobId);
        if (fileBytes == null)
            return NotFound("Print job not found or expired.");

        _pdfSecurity.CleanupJob(jobId);

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
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var watermarked = _watermarkService.AddHeavyWatermark(fs, shopName, userName, DateTime.UtcNow);
            var ms = new MemoryStream(watermarked);
            return File(ms, "application/pdf", enableRangeProcessing: false);
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

    [HttpPost("log-print/{bookId}")]
    [Authorize(Roles = "Shop")]
    public async Task<IActionResult> LogPrint(int bookId, [FromBody] PrintRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.ShopId == null)
            return Unauthorized();

        var hasAccess = await _db.ShopBookAssignments
            .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == bookId && a.IsActive);

        if (!hasAccess)
            return Forbid();

        var copies = Math.Max(1, request.Copies);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _printLogging.LogPrintAsync(
            user.ShopId.Value,
            bookId,
            copies,
            user.Id,
            user.UserName
        );

        _logger.LogInformation("Shop {ShopId} printed {Copies} copies of book {BookId} from {IP}", user.ShopId, copies, bookId, ipAddress);

        return Ok(new { message = "Print logged successfully", copies });
    }
}

public class ProcessPrintRequest
{
    public int BookId { get; set; }
    public int Copies { get; set; } = 1;
}

public class PrintRequest
{
    public int Copies { get; set; } = 1;
}
