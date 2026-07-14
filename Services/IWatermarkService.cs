namespace PrintingBooksPortal.Services;

public interface IWatermarkService
{
    byte[] AddHeavyWatermark(byte[] pdfBytes, string shopName, string userName, DateTime timestamp);
}
