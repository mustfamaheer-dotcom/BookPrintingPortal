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
    private readonly ILogger<SecurePdfController> _logger;

    public SecurePdfController(
        AppDbContext db,
        FileStorageService fileStorage,
        UserManager<ApplicationUser> userManager,
        PrintLoggingService printLogging,
        ILogger<SecurePdfController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _userManager = userManager;
        _printLogging = printLogging;
        _logger = logger;
    }

    [HttpGet("view/{bookId}")]
    [Authorize(Roles = "Shop,Admin")]
    public async Task<IActionResult> ViewPdf(int bookId)
    {
        var book = await _db.Books.Include(b => b.Board).FirstOrDefaultAsync(b => b.Id == bookId && b.IsActive);
        if (book == null)
            return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        if (!isAdmin)
        {
            var hasAccess = await _db.ShopBookAssignments
                .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == bookId && a.IsActive);

            if (!hasAccess)
                return Forbid();
        }

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        _logger.LogInformation("User {UserId} is viewing book {BookId} - {BookTitle}", user.Id, bookId, book.Title);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/pdf", enableRangeProcessing: false);
    }

    [HttpGet("print/{bookId}")]
    [Authorize(Roles = "Shop,Admin")]
    public async Task<IActionResult> PrintPdf(int bookId)
    {
        var book = await _db.Books.Include(b => b.Board).FirstOrDefaultAsync(b => b.Id == bookId && b.IsActive);
        if (book == null)
            return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (!isAdmin)
        {
            var hasAccess = await _db.ShopBookAssignments
                .AnyAsync(a => a.ShopId == user.ShopId && a.BookId == bookId && a.IsActive);
            if (!hasAccess)
                return Forbid();
        }

        var filePath = _fileStorage.GetFilePath(book.FilePath);
        if (!System.IO.File.Exists(filePath))
            return NotFound("PDF file not found on server.");

        _logger.LogInformation("User {UserId} is printing book {BookId} - {BookTitle}", user.Id, bookId, book.Title);

        try
        {
            var watermarkService = HttpContext.RequestServices.GetRequiredService<WatermarkService>();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var watermarked = watermarkService.AddWatermark(fs);
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

        await _printLogging.LogPrintAsync(
            user.ShopId.Value,
            bookId,
            copies,
            user.Id,
            user.UserName
        );

        _logger.LogInformation("Shop {ShopId} printed {Copies} copies of book {BookId}", user.ShopId, copies, bookId);

        return Ok(new { message = "Print logged successfully", copies });
    }
}

public class PrintRequest
{
    public int Copies { get; set; } = 1;
}
