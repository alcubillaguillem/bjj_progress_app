namespace BjjJournal.App.Ui;

/// <summary>Formato de fechas y cifras. Las fechas llegan en UTC y se pasan a hora local solo aquí.</summary>
public static class Format
{
    public static DateTimeOffset Local(DateTimeOffset utc) => utc.ToLocalTime();

    public static string Day(DateTimeOffset utc) => Local(utc).ToString("ddd d MMM");

    public static string DayLong(DateTimeOffset utc) => Local(utc).ToString("dddd, d 'de' MMMM 'de' yyyy · HH:mm");

    public static string Relative(DateTimeOffset utc)
    {
        var days = (DateTime.Today - Local(utc).Date).Days;
        return days switch
        {
            0 => "hoy",
            1 => "ayer",
            < 7 and > 1 => $"hace {days} días",
            _ => Local(utc).ToString("d MMM yyyy"),
        };
    }

    public static string Hours(double hours) => hours < 10 ? hours.ToString("0.#") : hours.ToString("0");

    public static string Minutes(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h{(minutes % 60 == 0 ? "" : $" {minutes % 60:00}")}";

    public static string WeekLabel(DateOnly weekStart) => weekStart.ToString("d/M");
}
