namespace BjjJournal.Application.Dtos;

/// <summary>
/// Mes en forma de calendario: semanas completas de lunes a domingo (incluye días del mes anterior y siguiente
/// para rellenar), con las sesiones de cada día y el resumen del mes.
/// </summary>
public sealed record CalendarMonth(
    int Year,
    int Month,
    IReadOnlyList<CalendarDay> Days,
    int Sessions,
    double Hours,
    int ActiveDays,
    int SubmissionsFor);

public sealed record CalendarDay(DateOnly Date, bool InMonth, IReadOnlyList<SessionListItem> Sessions);
