namespace AldaJoyeros.Helpers;

public sealed record MetricsPeriod(int Days, DateTime Start, DateTime End)
{
    public static DateTime MadridNow(TimeProvider? clock = null) => TimeZoneInfo.ConvertTime((clock ?? TimeProvider.System).GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")).DateTime;
    public static MetricsPeriod Create(DateTime today, int days) {
        days = days is 7 or 30 or 90 ? days : 30;
        return new(days, today.Date.AddDays(1-days), today.Date.AddDays(1));
    }
}
