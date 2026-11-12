using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record RollItem(Guid Id, string? PartnerName, Belt? PartnerBelt, int DurationMinutes, IReadOnlyList<RollEventItem> Events);
