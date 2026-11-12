using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record TechniqueListItem(
    Guid Id,
    string Name,
    TechniqueCategory Category,
    Guid? PositionId,
    string? PositionName,
    IReadOnlyList<string> Tags,
    ProgressStatus Progress,
    Belt? CurriculumBelt,
    int TimesWorked,
    DateTimeOffset? LastWorked);
