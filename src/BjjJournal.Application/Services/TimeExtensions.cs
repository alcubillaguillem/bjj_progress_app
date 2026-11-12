namespace BjjJournal.Application.Services;

/// <summary>Conversión de UTC a fecha local y cálculo de semanas (lunes a domingo).</summary>
internal static class TimeExtensions
{
    public static DateOnly LocalDate(this TimeProvider time, DateTimeOffset utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, time.LocalTimeZone).DateTime);

    public static DateOnly Today(this TimeProvider time) => time.LocalDate(time.GetUtcNow());

    public static DateOnly WeekStart(this DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // lunes = 0
        return date.AddDays(-offset);
    }
}
