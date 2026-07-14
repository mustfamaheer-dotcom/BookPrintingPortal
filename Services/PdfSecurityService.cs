using System.Text;
using iText.Kernel.Pdf;

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
        var ownerPassword = Guid.NewGuid().ToString("N")[..24];

        using var reader = new PdfReader(new MemoryStream(watermarkedBytes));
        using var ms = new MemoryStream();

        var writerProperties = new WriterProperties()
            .SetStandardEncryption(
                Encoding.UTF8.GetBytes(userPassword),
                Encoding.UTF8.GetBytes(ownerPassword),
                EncryptionConstants.ALLOW_PRINTING,
                EncryptionConstants.ENCRYPTION_AES_128
            );

        using var writer = new PdfWriter(ms, writerProperties);
        using var pdfDoc = new PdfDocument(reader, writer);
        pdfDoc.Close();

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
