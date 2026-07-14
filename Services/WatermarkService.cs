using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;

namespace PrintingBooksPortal.Services;

public class WatermarkService
{
    public byte[] AddWatermark(Stream pdfStream, string shopName, string userId, string userName)
    {
        using var inputDoc = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        using var outputDoc = new PdfDocument();

        string dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        string line1 = $"Licensed to: {shopName}";
        string line2 = $"User: {userName} ({userId})";
        string line3 = $"Printed: {dateStr}";

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var page = outputDoc.AddPage(inputDoc.Pages[i]);
            using var gfx = XGraphics.FromPdfPage(page);

            var font = new XFont("Arial", 36, XFontStyle.Bold);
            var brush = new XSolidBrush(XColor.FromArgb(30, 200, 0, 0));

            double cx = page.Width / 2;
            double cy = page.Height / 2;

            gfx.Save();
            gfx.TranslateTransform(cx, cy);
            gfx.RotateTransform(-45);

            gfx.DrawString(line1, font, brush, new XPoint(0, -40), XStringFormats.Center);
            gfx.DrawString(line2, font, brush, new XPoint(0, 10), XStringFormats.Center);
            gfx.DrawString(line3, font, brush, new XPoint(0, 60), XStringFormats.Center);

            gfx.Restore();
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms);
        return ms.ToArray();
    }
}
