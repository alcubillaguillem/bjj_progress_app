using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Services;

/// <summary>F5: las 6 métricas del dashboard y las lagunas del juego. Todo se calcula aquí, no en la UI.</summary>
public sealed class StatsService(JournalContext context, TimeProvider time)
{
    public const int WeeksShown = 12;
    public const int MovingAverageWeeks = 4;
    public const int GapDays = 30;

    public async Task<Dashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var sessions = data.Sessions.Active().OrderBy(s => s.StartedAt).ToList();
        var today = time.Today();
        var thisWeek = today.WeekStart();
        var thisWeekSessions = sessions.Where(s => time.LocalDate(s.StartedAt).WeekStart() == thisWeek).ToList();

        return new Dashboard(
            sessions.Count,
            sessions.Sum(s => s.DurationMinutes) / 60.0,
            thisWeekSessions.Count,
            thisWeekSessions.Sum(s => s.DurationMinutes) / 60.0,
            CurrentStreak(sessions, today),
            sessions.LastOrDefault()?.ToListItem(),
            Weekly(sessions, today),
            TopTechniques(sessions, data.Techniques),
            Submissions(sessions, data.Techniques),
            Enum.GetValues<Attire>().ToDictionary(a => a, a => sessions.Count(s => s.Attire == a)),
            Enum.GetValues<SessionType>().ToDictionary(t => t, t => sessions.Count(s => s.Type == t)),
            SatisfactionTrend(sessions, today),
            Gaps(sessions, data, today));
    }

    public const int RadarAxes = 6;

    /// <summary>
    /// Ranking de sumisiones (a favor o en contra) de un mes, o de todo el histórico si year/month son null.
    /// Con withPositions agrupa por técnica y posición del evento («Armbar · Montada»).
    /// </summary>
    public async Task<SubmissionBreakdown> GetSubmissionsAsync(
        int? year, int? month, RollEventKind kind = RollEventKind.SubmissionFor, bool withPositions = false, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var sessions = data.Sessions.Active()
            .Where(s => year is null || (time.LocalDate(s.StartedAt).Year == year && (month is null || time.LocalDate(s.StartedAt).Month == month)));
        return Breakdown(sessions, data, kind, withPositions);
    }

    public async Task<ProfileSummary> GetProfileSummaryAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var sessions = data.Sessions.Active().OrderBy(s => s.StartedAt).ToList();
        var minutes = sessions.Sum(s => s.DurationMinutes);
        var rolls = sessions.Sum(s => s.Rolls.Count(r => !r.IsDeleted));
        var subsFor = sessions.Sum(s => s.CountEvents(RollEventKind.SubmissionFor));
        var subsAgainst = sessions.Sum(s => s.CountEvents(RollEventKind.SubmissionAgainst));
        var mix = new SessionMix(
            sessions.Count(s => s.Attire == Attire.Gi),
            sessions.Count(s => s.Attire == Attire.NoGi),
            sessions.Count(s => s.Type is SessionType.Class or SessionType.Private),
            sessions.Count(s => s.Type == SessionType.OpenMat),
            sessions.Count(s => s.Type == SessionType.Drilling),
            sessions.Count(s => s.Type == SessionType.Competition));

        return new ProfileSummary(
            sessions.Count,
            minutes / 60.0,
            rolls,
            subsFor,
            subsAgainst,
            CurrentStreak(sessions, time.Today()),
            LevelFor(Experience(sessions.Count, minutes, rolls, subsFor)),
            Breakdown(sessions, data, RollEventKind.SubmissionFor, false).Items.Take(RadarAxes).ToList(),
            mix);
    }

    /// <summary>10 XP por sesión, 1 por cada 10 minutos, 1 por roll y 3 por sumisión a favor.</summary>
    public static int Experience(int sessions, int minutes, int rolls, int submissionsFor) =>
        (sessions * 10) + (minutes / 10) + rolls + (submissionsFor * 3);

    /// <summary>El nivel L empieza en 25·L·(L−1) XP: 0, 50, 150, 300, 500…</summary>
    public static LevelInfo LevelFor(int xp)
    {
        static int Start(int level) => 25 * level * (level - 1);
        var level = 1;
        while (Start(level + 1) <= xp)
        {
            level++;
        }

        return new LevelInfo(level, xp, Start(level), Start(level + 1));
    }

    private static SubmissionBreakdown Breakdown(IEnumerable<TrainingSession> sessions, AppData data, RollEventKind kind, bool withPositions)
    {
        var names = data.Techniques.ToDictionary(t => t.Id, t => t.Name);
        var positions = data.Positions.ToDictionary(p => p.Id, p => p.Name);
        var events = sessions
            .SelectMany(s => s.Rolls).Where(r => !r.IsDeleted)
            .SelectMany(r => r.Events)
            .Where(e => !e.IsDeleted && e.Kind == kind)
            .ToList();
        var total = events.Count;

        var items = events
            .GroupBy(e => (e.TechniqueId, PositionId: withPositions ? e.PositionId : null))
            .Select(g => new SubmissionSlice(
                g.Key.TechniqueId,
                g.Key.TechniqueId is { } id && names.TryGetValue(id, out var n) ? n : "Sin técnica",
                g.Key.PositionId is { } pid && positions.TryGetValue(pid, out var p) ? p : null,
                g.Count(),
                total == 0 ? 0 : (double)g.Count() / total))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new SubmissionBreakdown(total, events.Select(e => e.TechniqueId).Distinct().Count(), items);
    }

    /// <summary>1. Sesiones y horas por semana, últimas 12 semanas (la actual incluida).</summary>
    internal IReadOnlyList<WeeklyPoint> Weekly(IReadOnlyList<TrainingSession> sessions, DateOnly today)
    {
        var byWeek = sessions.GroupBy(s => time.LocalDate(s.StartedAt).WeekStart()).ToDictionary(g => g.Key, g => g.ToList());
        var first = today.WeekStart().AddDays(-7 * (WeeksShown - 1));
        return Enumerable.Range(0, WeeksShown)
            .Select(i => first.AddDays(7 * i))
            .Select(w => byWeek.TryGetValue(w, out var list)
                ? new WeeklyPoint(w, list.Count, list.Sum(s => s.DurationMinutes) / 60.0)
                : new WeeklyPoint(w, 0, 0))
            .ToList();
    }

    /// <summary>
    /// 2. Racha de semanas seguidas con al menos una sesión.
    /// Si esta semana aún no hay sesión, la racha cuenta hasta la semana pasada (no se rompe hasta que acaba la semana).
    /// </summary>
    internal int CurrentStreak(IReadOnlyList<TrainingSession> sessions, DateOnly today)
    {
        var weeks = sessions.Select(s => time.LocalDate(s.StartedAt).WeekStart()).ToHashSet();
        var week = today.WeekStart();
        if (!weeks.Contains(week))
        {
            week = week.AddDays(-7);
        }

        var streak = 0;
        while (weeks.Contains(week))
        {
            streak++;
            week = week.AddDays(-7);
        }

        return streak;
    }

    /// <summary>3. Top 10 técnicas por número de sesiones en las que aparecen.</summary>
    internal static IReadOnlyList<TechniqueCount> TopTechniques(IReadOnlyList<TrainingSession> sessions, IEnumerable<Technique> techniques)
    {
        var byId = techniques.ToDictionary(t => t.Id);
        return sessions.TechniqueUsage()
            .Where(kv => byId.ContainsKey(kv.Key))
            .Select(kv => new TechniqueCount(kv.Key, byId[kv.Key].Name, byId[kv.Key].Category, kv.Value.Count))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(10)
            .ToList();
    }

    /// <summary>4. Sumisiones a favor y en contra por técnica.</summary>
    internal static IReadOnlyList<SubmissionStat> Submissions(IReadOnlyList<TrainingSession> sessions, IEnumerable<Technique> techniques)
    {
        var names = techniques.ToDictionary(t => t.Id, t => t.Name);
        return sessions
            .SelectMany(s => s.Rolls).Where(r => !r.IsDeleted)
            .SelectMany(r => r.Events)
            .Where(e => !e.IsDeleted && e.Kind is RollEventKind.SubmissionFor or RollEventKind.SubmissionAgainst)
            .GroupBy(e => e.TechniqueId)
            .Select(g => new SubmissionStat(
                g.Key,
                g.Key is { } id && names.TryGetValue(id, out var n) ? n : "Sin técnica",
                g.Count(e => e.Kind == RollEventKind.SubmissionFor),
                g.Count(e => e.Kind == RollEventKind.SubmissionAgainst)))
            .OrderByDescending(s => s.For + s.Against)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>6. Satisfacción media móvil de 4 semanas, para cada una de las últimas 12 semanas.</summary>
    internal IReadOnlyList<TrendPoint> SatisfactionTrend(IReadOnlyList<TrainingSession> sessions, DateOnly today)
    {
        var withWeek = sessions.Select(s => (Week: time.LocalDate(s.StartedAt).WeekStart(), s.Satisfaction)).ToList();
        var first = today.WeekStart().AddDays(-7 * (WeeksShown - 1));
        return Enumerable.Range(0, WeeksShown)
            .Select(i => first.AddDays(7 * i))
            .Select(w =>
            {
                var from = w.AddDays(-7 * (MovingAverageWeeks - 1));
                var window = withWeek.Where(x => x.Week >= from && x.Week <= w).ToList();
                return new TrendPoint(w, window.Count == 0 ? null : Math.Round(window.Average(x => x.Satisfaction), 2));
            })
            .ToList();
    }

    /// <summary>Lagunas: categorías y posiciones (con técnicas en tu biblioteca) sin trabajar en los últimos 30 días.</summary>
    internal GameGaps Gaps(IReadOnlyList<TrainingSession> sessions, AppData data, DateOnly today)
    {
        var since = today.AddDays(-GapDays);
        var recentIds = sessions
            .Where(s => time.LocalDate(s.StartedAt) > since)
            .SelectMany(s => s.TechniqueIds())
            .ToHashSet();
        var library = data.Techniques.Active().ToList();
        var recent = data.Techniques.Where(t => recentIds.Contains(t.Id)).ToList();

        var categories = Enum.GetValues<TechniqueCategory>()
            .Where(c => library.Any(t => t.Category == c) && recent.All(t => t.Category != c))
            .ToList();

        var positionNames = data.Positions.ToDictionary(p => p.Id, p => p.Name);
        var workedPositions = recent.Where(t => t.PositionId is not null).Select(t => t.PositionId!.Value).ToHashSet();
        var positions = library
            .Where(t => t.PositionId is not null && !workedPositions.Contains(t.PositionId.Value))
            .Select(t => t.PositionId!.Value)
            .Distinct()
            .Where(positionNames.ContainsKey)
            .Select(id => positionNames[id])
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new GameGaps(categories, positions);
    }
}
