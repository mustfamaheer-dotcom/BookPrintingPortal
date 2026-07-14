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
    private readonly ILogger<SecurePdfController> _logger;

    public SecurePdfController(
        AppDbContext db,
        FileStorageService fileStorage,
        UserManager<ApplicationUser> userManager,
        PrintLoggingService printLogging,
        WatermarkService watermarkService,
        PrintTokenService printTokenService,
        ILogger<SecurePdfController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _userManager = userManager;
        _printLogging = printLogging;
        _watermarkService = watermarkService;
        _printTokenService = printTokenService;
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

    [HttpGet("view/{bookId}")]
    [Authorize(Roles = "Shop,Admin")]
    public async Task<IActionResult> ViewPdf(int bookId)
    {
        var (book, user) = await ValidateAccess(bookId);
        if (book == null || user == null)
            return NotFound();

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        var shop = user.ShopId != null ? await _db.Shops.FindAsync(user.ShopId.Value) : null;
        _logger.LogInformation("User {UserId} is viewing book {BookId} - {BookTitle}", user.Id, bookId, book.Title);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/pdf", enableRangeProcessing: false);
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
            var watermarked = _watermarkService.AddWatermark(fs, shopName, userId, userName);
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

public class PrintRequest
{
    public int Copies { get; set; } = 1;
}
