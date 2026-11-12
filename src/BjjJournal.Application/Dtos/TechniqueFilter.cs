using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record TechniqueFilter(
    string? Query = null,
    TechniqueCategory? Category = null,
    Guid? PositionId = null,
    string? Tag = null,
    Belt? CurriculumBelt = null);
