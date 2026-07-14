using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;

namespace PrintingBooksPortal.Services;

public class WatermarkService
{
    public byte[] AddWatermark(Stream pdfStream)
    {
        using var inputDoc = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        using var outputDoc = new PdfDocument();

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var page = outputDoc.AddPage(inputDoc.Pages[i]);
            using var gfx = XGraphics.FromPdfPage(page);

            var font = new XFont("Arial", 60, XFontStyle.Bold);
            var brush = new XSolidBrush(XColor.FromArgb(40, 180, 0, 0));

            double cx = page.Width / 2;
            double cy = page.Height / 2;

            gfx.Save();
            gfx.TranslateTransform(cx, cy);
            gfx.RotateTransform(-45);
            gfx.DrawString("SAMPLE COPY", font, brush, new XPoint(0, 0), XStringFormats.Center);
            gfx.Restore();
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms);
        return ms.ToArray();
    }
}
