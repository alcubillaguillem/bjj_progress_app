using BjjJournal.Application.Dtos;
using BjjJournal.Application.Services;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class ProfileStatsTests
{
    [Theory]
    [InlineData(0, 1, 0, 50)]
    [InlineData(49, 1, 0, 50)]
    [InlineData(50, 2, 50, 150)]
    [InlineData(150, 3, 150, 300)]
    [InlineData(4461, 13, 3900, 4550)]
    public void LevelFor_Thresholds(int xp, int level, int start, int next)
    {
        StatsService.LevelFor(xp).ShouldBe(new LevelInfo(level, xp, start, next));
    }

    [Fact]
    public void Experience_WeightsEachPart()
    {
        StatsService.Experience(sessions: 2, minutes: 180, rolls: 5, submissionsFor: 3).ShouldBe(20 + 18 + 5 + 9);
    }

    [Fact]
    public void LevelInfo_Progress_IsFractionOfCurrentLevel()
    {
        new LevelInfo(2, 100, 50, 150).Progress.ShouldBe(0.5);
    }

    [Fact]
    public async Task Submissions_FilterByMonth_RankAndShare()
    {
        var j = new TestJournal();
        var (kimura, triangle, mount) = await SeedAsync(j);
        var now = j.Time.GetUtcNow(); // noviembre 2026
        await j.Sessions.CreateAsync(WithSubs(now, (kimura, mount), (kimura, null), (triangle, null)));
        await j.Sessions.CreateAsync(WithSubs(now.AddMonths(-1), (triangle, null)));

        var nov = await j.Stats.GetSubmissionsAsync(2026, 11);
        nov.Total.ShouldBe(3);
        nov.Unique.ShouldBe(2);
        nov.Items[0].Name.ShouldBe("Kimura");
        nov.Items[0].Count.ShouldBe(2);
        nov.Items[0].Share.ShouldBe(2.0 / 3, 0.0001);
        nov.Items[0].Position.ShouldBeNull();

        (await j.Stats.GetSubmissionsAsync(null, null)).Total.ShouldBe(4);
        (await j.Stats.GetSubmissionsAsync(2026, 10)).Items.Single().Name.ShouldBe("Triángulo");
    }

    [Fact]
    public async Task Submissions_WithPositions_SplitsSameTechnique()
    {
        var j = new TestJournal();
        var (kimura, _, mount) = await SeedAsync(j);
        await j.Sessions.CreateAsync(WithSubs(j.Time.GetUtcNow(), (kimura, mount), (kimura, null)));

        var items = (await j.Stats.GetSubmissionsAsync(2026, 11, withPositions: true)).Items;
        items.Count.ShouldBe(2);
        items.ShouldContain(i => i.Position == "Montada");
        items.ShouldContain(i => i.Position == null);
    }

    [Fact]
    public async Task Submissions_Against_OnlyCountsAgainst()
    {
        var j = new TestJournal();
        var (kimura, _, _) = await SeedAsync(j);
        var input = WithSubs(j.Time.GetUtcNow(), (kimura, null));
        input.Rolls[0].Events.Add(new RollEventInput { Kind = RollEventKind.SubmissionAgainst, TechniqueId = kimura });
        input.Rolls[0].Events.Add(new RollEventInput { Kind = RollEventKind.SubmissionAgainst, TechniqueId = kimura });
        await j.Sessions.CreateAsync(input);

        (await j.Stats.GetSubmissionsAsync(null, null, RollEventKind.SubmissionAgainst)).Total.ShouldBe(2);
    }

    [Fact]
    public async Task ProfileSummary_CountsAndMix()
    {
        var j = new TestJournal();
        var (kimura, triangle, _) = await SeedAsync(j);
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(WithSubs(now, (kimura, null), (triangle, null)));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-7), minutes: 120, type: SessionType.OpenMat, attire: Attire.NoGi));

        var p = await j.Stats.GetProfileSummaryAsync();
        p.Sessions.ShouldBe(2);
        p.Hours.ShouldBe(3);
        p.Rolls.ShouldBe(1);
        p.SubmissionsFor.ShouldBe(2);
        p.StreakWeeks.ShouldBe(2);
        p.Mix.ShouldBe(new SessionMix(Gi: 1, NoGi: 1, Class: 1, OpenMat: 1, Drilling: 0, Competition: 0));
        p.TopSubmissions.Count.ShouldBe(2);
        p.Level.Xp.ShouldBe(StatsService.Experience(2, 180, 1, 2));
    }

    private static async Task<(Guid Kimura, Guid Triangle, Guid Mount)> SeedAsync(TestJournal j)
    {
        var kimura = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura", Category = TechniqueCategory.Submission })).Value;
        var triangle = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Triángulo", Category = TechniqueCategory.Submission })).Value;
        var mount = (await j.Positions.CreateAsync("Montada", PositionRole.Top)).Value.Id;
        return (kimura, triangle, mount);
    }

    private static SessionInput WithSubs(DateTimeOffset when, params (Guid Tech, Guid? Pos)[] subs)
    {
        var input = SessionBuilder.At(when);
        input.Rolls.Add(new RollInput
        {
            Events = subs.Select(s => new RollEventInput { Kind = RollEventKind.SubmissionFor, TechniqueId = s.Tech, PositionId = s.Pos }).ToList(),
        });
        return input;
    }
}
