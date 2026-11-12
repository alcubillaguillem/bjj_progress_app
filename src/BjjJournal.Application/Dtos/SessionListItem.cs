using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record SessionListItem(
    Guid Id,
    DateTimeOffset StartedAt,
    int DurationMinutes,
    SessionType Type,
    Attire Attire,
    string? Location,
    int Satisfaction,
    int TechniqueCount,
    int RollCount,
    int SubmissionsFor,
    int SubmissionsAgainst);
