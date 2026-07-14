namespace PrintingBooksPortal.Services;

public static class EgyptTime
{
    private static readonly TimeZoneInfo TZ = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

    public static DateTime ToEgyptLocal(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
            return utcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(
            utcDateTime.Kind == DateTimeKind.Local ? utcDateTime.ToUniversalTime() : utcDateTime, TZ);
    }
}
