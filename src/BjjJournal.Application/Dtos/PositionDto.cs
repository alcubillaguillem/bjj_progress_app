using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record PositionDto(Guid Id, string Name, PositionRole Role);
