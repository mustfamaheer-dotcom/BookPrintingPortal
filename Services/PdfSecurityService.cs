using iTextSharp.text.pdf;

namespace PrintingBooksPortal.Services;

public class PdfSecurityService
{
    private static readonly string SecureFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "SecurePrintJobs");

    public PdfSecurityService()
    {
        Directory.CreateDirectory(SecureFolder);
    }

    public string CreateSecurePrintFile(byte[] watermarkedBytes, string jobId, out string userPassword)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        userPassword = $"PRINT-{jobId}-{timestamp}";
        var ownerPassword = Guid.NewGuid().ToString("N").Substring(0, 24);

        using var reader = new PdfReader(watermarkedBytes);
        using var ms = new MemoryStream();

        using var stamper = new PdfStamper(reader, ms);

        int permissions = PdfWriter.ALLOW_PRINTING | PdfWriter.ALLOW_SCREENREADERS;

        stamper.Writer.SetEncryption(
            System.Text.Encoding.UTF8.GetBytes(userPassword),
            System.Text.Encoding.UTF8.GetBytes(ownerPassword),
            permissions,
            PdfWriter.STRENGTH128BITS
        );

        stamper.Close();

        var encryptedBytes = ms.ToArray();
        var filePath = Path.Combine(SecureFolder, $"{jobId}.pdf");
        File.WriteAllBytes(filePath, encryptedBytes);

        return filePath;
    }

    public byte[]? GetSecurePrintFile(string jobId)
    {
        var filePath = Path.Combine(SecureFolder, $"{jobId}.pdf");
        if (!File.Exists(filePath))
            return null;
        return File.ReadAllBytes(filePath);
    }

    public void CleanupJob(string jobId)
    {
        var filePath = Path.Combine(SecureFolder, $"{jobId}.pdf");
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public static void CleanupOldFiles(int olderThanMinutes = 30)
    {
        if (!Directory.Exists(SecureFolder)) return;
        foreach (var file in Directory.GetFiles(SecureFolder, "*.pdf"))
        {
            if (DateTime.UtcNow - File.GetCreationTimeUtc(file) > TimeSpan.FromMinutes(olderThanMinutes))
                try { File.Delete(file); } catch { }
        }
    }
}
