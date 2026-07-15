using BookShopPrintAgent.Services;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, 8080);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHttpClient<PdfPrintService>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseCors();
app.MapControllers();

var baseUrl = app.Configuration.GetValue<string>("ServerSettings:BaseUrl") ?? "https://drbaheegbook.runasp.net";
var apiKey = app.Configuration.GetValue<string>("ServerSettings:ApiKey") ?? "";
var printerName = app.Configuration.GetValue<string>("PrinterSettings:DefaultPrinterName") ?? "";
Console.WriteLine($"[BookShopPrintAgent] Listening on http://localhost:8080");
Console.WriteLine($"[BookShopPrintAgent] Server: {baseUrl}");
Console.WriteLine($"[BookShopPrintAgent] Polling for jobs every 3 seconds...");

_ = Task.Run(async () =>
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    var printService = app.Services.GetRequiredService<PdfPrintService>();

    while (true)
    {
        try
        {
            var pendingResponse = await client.GetAsync($"{baseUrl}/api/pdf/print-agent/pending");
            if (pendingResponse.IsSuccessStatusCode)
            {
                var json = await pendingResponse.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PendingResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result?.Jobs != null)
                {
                    foreach (var jobId in result.Jobs)
                    {
                        Console.WriteLine($"[BookShopPrintAgent] Found pending job: {jobId}");

                        try
                        {
                            var downloadUrl = $"{baseUrl.TrimEnd('/')}/api/pdf/download-secured/{jobId}";
                            var dlRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                            if (!string.IsNullOrEmpty(apiKey))
                                dlRequest.Headers.Add("X-Api-Key", apiKey);
                            var dlResponse = await client.SendAsync(dlRequest);
                            dlResponse.EnsureSuccessStatusCode();
                            var encryptedBytes = await dlResponse.Content.ReadAsByteArrayAsync();

                            var claimResponse = await client.PostAsync($"{baseUrl}/api/pdf/print-agent/claim/{jobId}", null);
                            if (!claimResponse.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[BookShopPrintAgent] Claim failed for {jobId} (already claimed), skipping");
                                continue;
                            }

                            var claimJson = await claimResponse.Content.ReadAsStringAsync();
                            var claimResult = JsonSerializer.Deserialize<ClaimResponse>(claimJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            var copies = claimResult?.Copies ?? 1;
                            Console.WriteLine($"[BookShopPrintAgent] Printing job {jobId}, {copies} copy(ies)...");

                            try
                            {
                                await printService.PrintAsync(encryptedBytes, jobId, printerName, copies);
                                Console.WriteLine($"[BookShopPrintAgent] Job {jobId} completed successfully");
                            }
                            catch (Exception printEx)
                            {
                                Console.WriteLine($"[BookShopPrintAgent] Print failed for job {jobId}: {printEx.Message}, releasing job back to queue");
                                try
                                {
                                    await client.PostAsync($"{baseUrl}/api/pdf/print-agent/release/{jobId}", null);
                                }
                                catch { /* best effort */ }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BookShopPrintAgent] Failed to process job {jobId}: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BookShopPrintAgent] Polling error: {ex.Message}");
        }

        await Task.Delay(3000);
    }
});

app.Run();

public class PendingResponse
{
    public List<string> Jobs { get; set; } = new();
}

public class ClaimResponse
{
    public bool Success { get; set; }
    public string JobId { get; set; } = "";
    public int Copies { get; set; } = 1;
}
