using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record WeeklyPoint(DateOnly WeekStart, int Sessions, double Hours);
