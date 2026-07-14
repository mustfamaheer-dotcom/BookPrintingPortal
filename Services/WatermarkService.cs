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

    public byte[] AddHeavyWatermark(Stream pdfStream, string shopName, string userName, DateTime timestamp)
    {
        using var inputDoc = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        using var outputDoc = new PdfDocument();

        string dateStr = timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC");
        string watermarkText = $"LICENSED TO {shopName} | USER: {userName} | DATE: {dateStr} | DO NOT DISTRIBUTE";

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var page = outputDoc.AddPage(inputDoc.Pages[i]);
            using var gfx = XGraphics.FromPdfPage(page);

            var font = new XFont("Arial", 72, XFontStyle.Bold);
            var brush = new XSolidBrush(XColor.FromArgb(25, 220, 0, 0));

            double cx = page.Width / 2;
            double cy = page.Height / 2;

            gfx.Save();
            gfx.TranslateTransform(cx, cy);
            gfx.RotateTransform(-45);

            gfx.DrawString(watermarkText, font, brush, new XPoint(0, 0), XStringFormats.Center);

            var font2 = new XFont("Arial", 18, XFontStyle.Regular);
            var brush2 = new XSolidBrush(XColor.FromArgb(40, 100, 100, 100));
            gfx.DrawString($"Printed via PrintingBooksPortal | {dateStr}", font2, brush2, new XPoint(0, cy * 0.6), XStringFormats.Center);

            gfx.Restore();

            using var gfx2 = XGraphics.FromPdfPage(page);
            var font3 = new XFont("Arial", 8, XFontStyle.Regular);
            var brush3 = new XSolidBrush(XColor.FromArgb(60, 80, 80, 80));
            gfx2.DrawString($"User: {userName} | Shop: {shopName} | {dateStr}", font3, brush3,
                new XPoint(page.Width - 10, page.Height - 10), XStringFormats.BottomRight);
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms);
        return ms.ToArray();
    }
}
