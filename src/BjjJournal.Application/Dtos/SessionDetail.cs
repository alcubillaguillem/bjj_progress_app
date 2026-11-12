using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record SessionDetail(
    Guid Id,
    DateTimeOffset StartedAt,
    int DurationMinutes,
    SessionType Type,
    Attire Attire,
    string? Location,
    int Satisfaction,
    string? Notes,
    IReadOnlyList<SessionTechniqueItem> Techniques,
    IReadOnlyList<RollItem> Rolls,
    int SubmissionsFor,
    int SubmissionsAgainst);
