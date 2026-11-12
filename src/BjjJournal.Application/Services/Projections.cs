using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Services;

/// <summary>Conversión de entidades a DTOs y cálculos de uso compartidos entre servicios.</summary>
internal static class Projections
{
    public static IEnumerable<TrainingSession> Active(this IEnumerable<TrainingSession> sessions) =>
        sessions.Where(s => !s.IsDeleted);

    public static IEnumerable<Technique> Active(this IEnumerable<Technique> techniques) =>
        techniques.Where(t => !t.IsDeleted);

    public static int CountEvents(this TrainingSession session, RollEventKind kind) =>
        session.Rolls.Where(r => !r.IsDeleted).SelectMany(r => r.Events).Count(e => !e.IsDeleted && e.Kind == kind);

    /// <summary>Ids de técnicas que aparecen en una sesión, ya sea en la lista de técnicas o en eventos de rolls.</summary>
    public static HashSet<Guid> TechniqueIds(this TrainingSession session)
    {
        var ids = session.Techniques.Select(t => t.TechniqueId).ToHashSet();
        foreach (var e in session.Rolls.Where(r => !r.IsDeleted).SelectMany(r => r.Events))
        {
            if (!e.IsDeleted && e.TechniqueId is { } id)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>Veces trabajada (sesiones distintas) y fecha de la última sesión por técnica.</summary>
    public static Dictionary<Guid, (int Count, DateTimeOffset Last)> TechniqueUsage(this IEnumerable<TrainingSession> sessions)
    {
        var usage = new Dictionary<Guid, (int Count, DateTimeOffset Last)>();
        foreach (var session in sessions.Active())
        {
            foreach (var id in session.TechniqueIds())
            {
                usage[id] = usage.TryGetValue(id, out var u)
                    ? (u.Count + 1, u.Last > session.StartedAt ? u.Last : session.StartedAt)
                    : (1, session.StartedAt);
            }
        }

        return usage;
    }

    public static SessionListItem ToListItem(this TrainingSession s) => new(
        s.Id,
        s.StartedAt,
        s.DurationMinutes,
        s.Type,
        s.Attire,
        s.Location,
        s.Satisfaction,
        s.Techniques.Count,
        s.Rolls.Count(r => !r.IsDeleted),
        s.CountEvents(RollEventKind.SubmissionFor),
        s.CountEvents(RollEventKind.SubmissionAgainst));

    public static List<string> CleanList(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(v => v.Trim().TrimStart('#'))
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? CleanText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
