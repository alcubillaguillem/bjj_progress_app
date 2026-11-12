using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

/// <summary>Hoy es miércoles 11/11/2026; la semana actual empieza el lunes 9/11.</summary>
public class StatsServiceTests
{
    [Fact]
    public async Task Dashboard_NoSessions_AllZero()
    {
        var d = await new TestJournal().Stats.GetDashboardAsync();
        d.TotalSessions.ShouldBe(0);
        d.CurrentStreakWeeks.ShouldBe(0);
        d.Weekly.Count.ShouldBe(12);
        d.LastSession.ShouldBeNull();
    }

    [Fact]
    public async Task Weekly_SessionsThisAndLastWeek_GroupedByMondayWeek()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(SessionBuilder.At(now, minutes: 90));                // mié 11/11
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-2), minutes: 30));    // lun 9/11
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-3), minutes: 60));    // dom 8/11 → semana anterior

        var d = await j.Stats.GetDashboardAsync();
        d.Weekly[^1].WeekStart.ShouldBe(new DateOnly(2026, 11, 9));
        d.Weekly[^1].Sessions.ShouldBe(2);
        d.Weekly[^1].Hours.ShouldBe(2);
        d.Weekly[^2].Sessions.ShouldBe(1);
        d.SessionsThisWeek.ShouldBe(2);
        d.HoursThisWeek.ShouldBe(2);
    }

    [Fact]
    public async Task Streak_ConsecutiveWeeksWithGap_CountsUntilGap()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        foreach (var weeksAgo in new[] { 0, 1, 2, 4 })
        {
            await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-7 * weeksAgo)));
        }

        (await j.Stats.GetDashboardAsync()).CurrentStreakWeeks.ShouldBe(3);
    }

    [Fact]
    public async Task Streak_NothingThisWeekYet_KeepsStreakFromLastWeek()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-7)));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-14)));

        (await j.Stats.GetDashboardAsync()).CurrentStreakWeeks.ShouldBe(2);
    }

    [Fact]
    public async Task Streak_LastSessionTwoWeeksAgo_IsZero()
    {
        var j = new TestJournal();
        await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow().AddDays(-15)));
        (await j.Stats.GetDashboardAsync()).CurrentStreakWeeks.ShouldBe(0);
    }

    [Fact]
    public async Task TopTechniques_CountsDistinctSessionsIncludingRollEvents()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        var kimura = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura", Category = TechniqueCategory.Submission })).Value;
        var tijera = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Tijera" })).Value;
        await j.Sessions.CreateAsync(SessionBuilder.At(now, tech: kimura));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-1), tech: tijera));
        var withRoll = SessionBuilder.At(now.AddDays(-2));
        withRoll.Rolls.Add(new RollInput { Events = [new RollEventInput { Kind = RollEventKind.SubmissionFor, TechniqueId = kimura }] });
        await j.Sessions.CreateAsync(withRoll);

        var top = (await j.Stats.GetDashboardAsync()).TopTechniques;
        top[0].Name.ShouldBe("Kimura");
        top[0].Count.ShouldBe(2);
        top[1].Count.ShouldBe(1);
    }

    [Fact]
    public async Task Submissions_ForAndAgainstPerTechnique()
    {
        var j = new TestJournal();
        var kimura = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        var input = SessionBuilder.At(j.Time.GetUtcNow());
        input.Rolls.Add(new RollInput
        {
            Events =
            [
                new RollEventInput { Kind = RollEventKind.SubmissionFor, TechniqueId = kimura },
                new RollEventInput { Kind = RollEventKind.SubmissionAgainst, TechniqueId = kimura },
                new RollEventInput { Kind = RollEventKind.SubmissionAgainst },
            ],
        });
        await j.Sessions.CreateAsync(input);

        var subs = (await j.Stats.GetDashboardAsync()).Submissions;
        subs.Single(s => s.TechniqueId == kimura).ShouldBe(new SubmissionStat(kimura, "Kimura", 1, 1));
        subs.Single(s => s.TechniqueId is null).Against.ShouldBe(1);
    }

    [Fact]
    public async Task Splits_CountAttireAndType()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(SessionBuilder.At(now, attire: Attire.NoGi, type: SessionType.OpenMat));
        await j.Sessions.CreateAsync(SessionBuilder.At(now));
        await j.Sessions.CreateAsync(SessionBuilder.At(now));

        var d = await j.Stats.GetDashboardAsync();
        d.AttireSplit[Attire.Gi].ShouldBe(2);
        d.AttireSplit[Attire.NoGi].ShouldBe(1);
        d.TypeSplit[SessionType.OpenMat].ShouldBe(1);
        d.TypeSplit[SessionType.Class].ShouldBe(2);
    }

    [Fact]
    public async Task SatisfactionTrend_FourWeekMovingAverage()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-35), satisfaction: 1)); // 5 semanas atrás: fuera de la ventana actual
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-14), satisfaction: 2));
        await j.Sessions.CreateAsync(SessionBuilder.At(now, satisfaction: 4));

        var trend = (await j.Stats.GetDashboardAsync()).SatisfactionTrend;
        trend[^1].Average.ShouldBe(3);       // (2 + 4) / 2
        trend[^2].Average.ShouldBe(2);       // semana -1: ventana -4..-1, solo la de -2
        trend[^3].Average.ShouldBe(1.5);     // semana -2: ventana -5..-2, (1 + 2) / 2
        trend[^4].Average.ShouldBe(1);       // semana -3: ventana -6..-3, solo la de -5
        trend[0].Average.ShouldBeNull();
    }

    [Fact]
    public async Task Gaps_CategoryNotWorkedIn30Days_IsReported()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        var sub = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura", Category = TechniqueCategory.Submission })).Value;
        var esc = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Upa", Category = TechniqueCategory.Escape })).Value;
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-3), tech: sub));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-45), tech: esc));

        var gaps = (await j.Stats.GetDashboardAsync()).Gaps;
        gaps.Categories.ShouldBe([TechniqueCategory.Escape]);
    }
}
