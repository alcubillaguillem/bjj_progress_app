using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record GameGaps(IReadOnlyList<TechniqueCategory> Categories, IReadOnlyList<string> Positions);
