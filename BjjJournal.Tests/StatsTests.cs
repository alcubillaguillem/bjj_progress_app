namespace BjjJournal.Tests;

/// <summary>Cálculos de estadísticas con fechas fijas (hoy = miércoles 14/01/2026).</summary>
public class StatsTests
{
    private static readonly DateOnly Today = new(2026, 1, 14);

    private static TrainingSession At(DateOnly day, int minutes = 60, int satisfaction = 3, params RollEvent[] events) => new()
    {
        StartedAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0))),
        DurationMinutes = minutes,
        Satisfaction = satisfaction,
        Rolls = events.Length == 0 ? [] : [new Roll { Events = [.. events] }],
    };

    [Fact]
    public void WeekStart_IsMonday() => Assert.Equal(new DateOnly(2026, 1, 12), Stats.WeekStart(Today));

    [Fact]
    public void Streak_CountsConsecutiveWeeksUntilAGap()
    {
        var sessions = new[] { At(Today), At(Today.AddDays(-7)), At(Today.AddDays(-14)), At(Today.AddDays(-35)) };
        Assert.Equal(3, Stats.Streak(sessions, Today));
    }

    [Fact]
    public void Streak_NothingThisWeekYet_KeepsLastWeeksStreak()
    {
        Assert.Equal(1, Stats.Streak([At(Today.AddDays(-7))], Today));
        Assert.Equal(0, Stats.Streak([At(Today.AddDays(-14))], Today));
    }

    [Fact]
    public void Weekly_TwelveWeeksEndingThisWeek()
    {
        var weeks = Stats.Weekly([At(Today, 90), At(Today.AddDays(-1), 30), At(Today.AddDays(-7))], Today);

        Assert.Equal(12, weeks.Count);
        Assert.Equal(new WeekPoint(new DateOnly(2026, 1, 12), 2, 2), weeks[^1]);
        Assert.Equal(1, weeks[^2].Sessions);
    }

    [Fact]
    public void SatisfactionTrend_IsFourWeekMovingAverage()
    {
        var trend = Stats.SatisfactionTrend([At(Today, satisfaction: 5), At(Today.AddDays(-21), satisfaction: 1), At(Today.AddDays(-28), satisfaction: 1)], Today);
        Assert.Equal(3, trend[^1].Average); // (5 + 1) / 2: la de hace 4 semanas ya no entra
        Assert.Null(trend[0].Average);
    }

    [Fact]
    public async Task TopTechniques_CountsSessionsIncludingRollEvents()
    {
        var journal = new Journal();
        var kimura = new Technique { Name = "Kimura" };
        await journal.SaveTechniqueAsync(kimura);
        var s1 = At(Today);
        s1.Techniques.Add(new() { TechniqueId = kimura.Id });
        await journal.SaveSessionAsync(s1);
        await journal.SaveSessionAsync(At(Today, events: new RollEvent { TechniqueId = kimura.Id }));

        Assert.Equal(2, Stats.TopTechniques(journal).Single().Count);
    }

    [Fact]
    public async Task Submissions_RankWithShareAndOptionalPositions()
    {
        var journal = new Journal();
        var armbar = new Technique { Name = "Armbar" };
        await journal.SaveTechniqueAsync(armbar);
        var mount = await journal.AddPositionAsync("Montada");
        var guard = await journal.AddPositionAsync("Guardia");
        await journal.SaveSessionAsync(At(Today, events:
        [
            new() { Kind = RollEventKind.SubmissionFor, TechniqueId = armbar.Id, PositionId = mount.Id },
            new() { Kind = RollEventKind.SubmissionFor, TechniqueId = armbar.Id, PositionId = guard.Id },
            new() { Kind = RollEventKind.SubmissionFor },
            new() { Kind = RollEventKind.SubmissionAgainst, TechniqueId = armbar.Id },
        ]));

        var ranking = Stats.Submissions(journal, journal.Sessions, RollEventKind.SubmissionFor);
        Assert.Equal((3, 2), (ranking.Total, ranking.Unique));
        Assert.Equal(new RankItem("Armbar", null, 2, 2 / 3.0), ranking.Items[0]);
        Assert.Equal("Sin técnica", ranking.Items[1].Name);

        Assert.Equal(3, Stats.Submissions(journal, journal.Sessions, RollEventKind.SubmissionFor, withPositions: true).Items.Count);
        Assert.Equal(("Armbar", 2, 1), Stats.SubmissionsByTechnique(journal)[0]);
    }

    [Fact]
    public async Task Gaps_CategoriesAndPositionsNotWorkedIn30Days()
    {
        var journal = new Journal();
        var mount = await journal.AddPositionAsync("Montada");
        var sweep = new Technique { Name = "Tijera", Category = TechniqueCategory.Sweep };
        await journal.SaveTechniqueAsync(sweep);
        await journal.SaveTechniqueAsync(new Technique { Name = "Americana", Category = TechniqueCategory.Submission, PositionId = mount.Id });
        var s = At(Today.AddDays(-3));
        s.Techniques.Add(new() { TechniqueId = sweep.Id });
        await journal.SaveSessionAsync(s);

        var (categories, positions) = Stats.Gaps(journal, Today);
        Assert.Equal([TechniqueCategory.Submission], categories);
        Assert.Equal(["Montada"], positions);
    }

    [Fact]
    public void Level_GrowsWithExperience()
    {
        // 1 sesión de 60 min con un roll y una sumisión: 10 + 6 + 1 + 3 = 20 XP
        Assert.Equal(20, Stats.Experience([At(Today, 60, 3, new RollEvent { Kind = RollEventKind.SubmissionFor })]));
        Assert.Equal(1, Stats.LevelFor(49).Number);
        Assert.Equal(new Level(2, 100, 50, 150), Stats.LevelFor(100));
        Assert.Equal(0.5, Stats.LevelFor(100).Progress);
    }
}
