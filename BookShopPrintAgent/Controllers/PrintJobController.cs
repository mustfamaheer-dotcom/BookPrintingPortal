using BookShopPrintAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookShopPrintAgent.Controllers;

[ApiController]
[Route("api/print-job")]
public class PrintJobController : ControllerBase
{
    private readonly PdfPrintService _printService;
    private readonly IConfiguration _config;
    private readonly ILogger<PrintJobController> _logger;

    public PrintJobController(PdfPrintService printService, IConfiguration config, ILogger<PrintJobController> logger)
    {
        _printService = printService;
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitPrintJob([FromBody] PrintJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobId))
            return BadRequest(new { success = false, error = "JobId is required." });

        _logger.LogInformation("Received print job: {JobId}, Copies: {Copies}", request.JobId, request.Copies);

        try
        {
            var copies = Math.Max(1, request.Copies);
            var printerName = _config.GetValue<string>("PrinterSettings:DefaultPrinterName") ?? "";

            await _printService.DownloadAndPrintAsync(request.JobId, printerName, copies);

            _logger.LogInformation("Print job completed: {JobId}", request.JobId);

            return Ok(new
            {
                success = true,
                jobId = request.JobId,
                message = $"Print job {request.JobId} sent to printer ({copies} copy(ies))."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print job failed: {JobId}", request.JobId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "running", timestamp = DateTime.UtcNow });
    }
}

public class PrintJobRequest
{
    public string JobId { get; set; } = "";
    public int Copies { get; set; } = 1;
}
