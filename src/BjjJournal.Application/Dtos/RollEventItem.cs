using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record RollEventItem(RollEventKind Kind, Guid? TechniqueId, string? TechniqueName, Guid? PositionId, string? PositionName);
