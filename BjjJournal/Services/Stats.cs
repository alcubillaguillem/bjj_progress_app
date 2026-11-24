using BjjJournal.Models;

namespace BjjJournal.Services;

/// <summary>Cálculos de las estadísticas (inicio, perfil y sumisiones). Funciones puras: reciben datos y devuelven resultados.</summary>
public static class Stats
{
    public const int WeeksShown = 12;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Lunes de la semana de esa fecha.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static double Hours(IEnumerable<TrainingSession> sessions) => sessions.Sum(s => s.DurationMinutes) / 60.0;

    /// <summary>
    /// Semanas seguidas con al menos una sesión. Si esta semana aún no has entrenado,
    /// cuenta hasta la semana pasada (la racha no se rompe hasta que acaba la semana).
    /// </summary>
    public static int Streak(IEnumerable<TrainingSession> sessions, DateOnly today)
    {
        var weeks = sessions.Select(s => WeekStart(s.Day)).ToHashSet();
        var week = WeekStart(today);
        if (!weeks.Contains(week))
        {
            week = week.AddDays(-7);
        }

        var streak = 0;
        for (; weeks.Contains(week); week = week.AddDays(-7))
        {
            streak++;
        }

        return streak;
    }

    /// <summary>Sesiones y horas de cada una de las últimas 12 semanas (la actual incluida).</summary>
    public static List<WeekPoint> Weekly(IEnumerable<TrainingSession> sessions, DateOnly today) =>
        LastWeeks(today).Select(w =>
        {
            var inWeek = sessions.Where(s => WeekStart(s.Day) == w).ToList();
            return new WeekPoint(w, inWeek.Count, Hours(inWeek));
        }).ToList();

    /// <summary>Satisfacción media de las 4 semanas anteriores (media móvil), para cada una de las últimas 12 semanas.</summary>
    public static List<(DateOnly Week, double? Average)> SatisfactionTrend(IEnumerable<TrainingSession> sessions, DateOnly today) =>
        LastWeeks(today).Select(w =>
        {
            var window = sessions.Where(s => WeekStart(s.Day) <= w && WeekStart(s.Day) >= w.AddDays(-21)).ToList();
            return (w, window.Count == 0 ? (double?)null : Math.Round(window.Average(s => s.Satisfaction), 2));
        }).ToList();

    /// <summary>Las 10 técnicas que aparecen en más sesiones.</summary>
    public static List<(Technique Technique, int Count)> TopTechniques(Journal journal) =>
        journal.Usage()
            .Select(u => (Technique: journal.Data.Techniques.FirstOrDefault(t => t.Id == u.Key), u.Value.Count))
            .Where(x => x.Technique is not null)
            .OrderByDescending(x => x.Count).ThenBy(x => x.Technique!.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(10)
            .ToList()!;

    /// <summary>Sumisiones a favor y en contra de cada técnica.</summary>
    public static List<(string Name, int For, int Against)> SubmissionsByTechnique(Journal journal) =>
        journal.Sessions.SelectMany(s => s.Events)
            .Where(e => e.Kind is RollEventKind.SubmissionFor or RollEventKind.SubmissionAgainst)
            .GroupBy(e => e.TechniqueId)
            .Select(g => (Name: journal.TechniqueName(g.Key) ?? "Sin técnica",
                          For: g.Count(e => e.Kind == RollEventKind.SubmissionFor),
                          Against: g.Count(e => e.Kind == RollEventKind.SubmissionAgainst)))
            .OrderByDescending(x => x.For + x.Against).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Ranking de sumisiones de un tipo. Con posiciones, separa la misma técnica por posición («Armbar · Montada»).</summary>
    public static Ranking Submissions(Journal journal, IEnumerable<TrainingSession> sessions, RollEventKind kind, bool withPositions = false)
    {
        var events = sessions.SelectMany(s => s.Events).Where(e => e.Kind == kind).ToList();
        var items = events
            .GroupBy(e => (e.TechniqueId, PositionId: withPositions ? e.PositionId : null))
            .Select(g => new RankItem(
                journal.TechniqueName(g.Key.TechniqueId) ?? "Sin técnica",
                journal.PositionName(g.Key.PositionId),
                g.Count(),
                (double)g.Count() / events.Count))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new Ranking(events.Count, events.Select(e => e.TechniqueId).Distinct().Count(), items);
    }

    /// <summary>Lagunas: categorías y posiciones de tu biblioteca que no has trabajado en los últimos 30 días.</summary>
    public static (List<TechniqueCategory> Categories, List<string> Positions) Gaps(Journal journal, DateOnly today)
    {
        var recentIds = journal.Sessions.Where(s => s.Day > today.AddDays(-30)).SelectMany(s => s.TechniqueIds()).ToHashSet();
        var library = journal.Techniques.ToList();
        var recent = journal.Data.Techniques.Where(t => recentIds.Contains(t.Id)).ToList();

        var categories = Enum.GetValues<TechniqueCategory>()
            .Where(c => library.Any(t => t.Category == c) && recent.All(t => t.Category != c))
            .ToList();
        var positions = library
            .Where(t => t.PositionId is not null && recent.All(r => r.PositionId != t.PositionId))
            .Select(t => journal.PositionName(t.PositionId))
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return (categories, positions);
    }

    /// <summary>10 XP por sesión, 1 por cada 10 minutos, 1 por roll y 3 por sumisión a favor.</summary>
    public static int Experience(IReadOnlyCollection<TrainingSession> sessions) =>
        (sessions.Count * 10)
        + (sessions.Sum(s => s.DurationMinutes) / 10)
        + sessions.Sum(s => s.Rolls.Count)
        + (sessions.Sum(s => s.Count(RollEventKind.SubmissionFor)) * 3);

    /// <summary>El nivel N empieza en 25·N·(N−1) XP: 0, 50, 150, 300, 500…</summary>
    public static Level LevelFor(int xp)
    {
        static int Start(int level) => 25 * level * (level - 1);
        var level = 1;
        while (Start(level + 1) <= xp)
        {
            level++;
        }

        return new Level(level, xp, Start(level), Start(level + 1));
    }

    private static IEnumerable<DateOnly> LastWeeks(DateOnly today) =>
        Enumerable.Range(0, WeeksShown).Select(i => WeekStart(today).AddDays(-7 * (WeeksShown - 1 - i)));
}

public record WeekPoint(DateOnly Week, int Sessions, double Hours);

public record RankItem(string Name, string? Position, int Count, double Share);

public record Ranking(int Total, int Unique, List<RankItem> Items);

public record Level(int Number, int Xp, int StartXp, int NextXp)
{
    public double Progress => (double)(Xp - StartXp) / (NextXp - StartXp);
}
