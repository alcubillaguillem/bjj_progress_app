using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record Dashboard(
    int TotalSessions,
    double TotalHours,
    int SessionsThisWeek,
    double HoursThisWeek,
    int CurrentStreakWeeks,
    SessionListItem? LastSession,
    IReadOnlyList<WeeklyPoint> Weekly,
    IReadOnlyList<TechniqueCount> TopTechniques,
    IReadOnlyList<SubmissionStat> Submissions,
    IReadOnlyDictionary<Attire, int> AttireSplit,
    IReadOnlyDictionary<SessionType, int> TypeSplit,
    IReadOnlyList<TrendPoint> SatisfactionTrend,
    GameGaps Gaps);
