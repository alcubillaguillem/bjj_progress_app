using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record TechniqueDetail(
    Guid Id,
    string Name,
    TechniqueCategory Category,
    Guid? PositionId,
    string? PositionName,
    Belt? CurriculumBelt,
    ProgressStatus Progress,
    string? Description,
    string? Notes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Links,
    int TimesWorked,
    DateTimeOffset? LastWorked,
    int SubmissionsFor,
    int SubmissionsAgainst,
    IReadOnlyList<SessionListItem> RecentSessions);
