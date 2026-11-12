using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record SessionFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    SessionType? Type = null,
    Attire? Attire = null,
    int? MinSatisfaction = null);
