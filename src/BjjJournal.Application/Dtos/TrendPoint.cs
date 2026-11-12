using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record TrendPoint(DateOnly WeekStart, double? Average);
