using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

/// <summary>Hoy es miércoles 11/11/2026. Noviembre 2026 empieza en domingo y acaba en lunes.</summary>
public class CalendarTests
{
    [Fact]
    public async Task Calendar_FullWeeksFromMondayToSunday()
    {
        var cal = await new TestJournal().Sessions.GetCalendarAsync(2026, 11);
        cal.Days[0].Date.ShouldBe(new DateOnly(2026, 10, 26));   // lunes anterior al 1/11
        cal.Days[^1].Date.ShouldBe(new DateOnly(2026, 12, 6));   // domingo posterior al 30/11
        (cal.Days.Count % 7).ShouldBe(0);
        cal.Days.Count(d => d.InMonth).ShouldBe(30);
    }

    [Fact]
    public async Task Calendar_GroupsSessionsByDayAndSummarizesMonth()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        var input = SessionBuilder.At(now, minutes: 90);
        input.Rolls.Add(new() { Events = [new() { Kind = RollEventKind.SubmissionFor }] });
        await j.Sessions.CreateAsync(input);
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddHours(-3), minutes: 30, type: SessionType.OpenMat));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-2)));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-20)));  // 22/10: visible en la rejilla, fuera del mes

        var cal = await j.Sessions.GetCalendarAsync(2026, 11);
        var day = cal.Days.Single(d => d.Date == new DateOnly(2026, 11, 11));
        day.Sessions.Count.ShouldBe(2);
        day.Sessions[0].Type.ShouldBe(SessionType.OpenMat);   // ordenadas por hora
        cal.Sessions.ShouldBe(3);
        cal.Hours.ShouldBe(3);
        cal.ActiveDays.ShouldBe(2);
        cal.SubmissionsFor.ShouldBe(1);
    }

    [Fact]
    public async Task Calendar_PaddingDaysShowSessionsButDontCount()
    {
        var j = new TestJournal();
        await j.Sessions.CreateAsync(SessionBuilder.At(new DateTimeOffset(2026, 10, 27, 19, 0, 0, TimeSpan.Zero)));

        var cal = await j.Sessions.GetCalendarAsync(2026, 11);
        cal.Days.Single(d => d.Date == new DateOnly(2026, 10, 27)).Sessions.Count.ShouldBe(1);
        cal.Sessions.ShouldBe(0);
    }

    [Fact]
    public async Task Defaults_ForDate_UsesThatDayAndLastSessionHour()
    {
        var j = new TestJournal();
        await j.Sessions.CreateAsync(SessionBuilder.At(new DateTimeOffset(2026, 11, 9, 19, 30, 0, TimeSpan.Zero)));

        var d = await j.Sessions.GetDefaultsAsync(new DateOnly(2026, 11, 3));
        d.StartedAt.ShouldBe(new DateTimeOffset(2026, 11, 3, 19, 30, 0, TimeSpan.Zero));
    }
}
