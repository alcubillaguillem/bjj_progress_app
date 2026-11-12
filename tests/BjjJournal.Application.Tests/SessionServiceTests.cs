using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class SessionServiceTests
{
    [Fact]
    public async Task CreateAsync_MinimalSession_IsSavedToStore()
    {
        var j = new TestJournal();
        var result = await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow()));
        result.IsSuccess.ShouldBeTrue();
        j.Store.Writes.ShouldBe(1);
        j.Store.Json!.ShouldContain(result.Value.ToString());
    }

    [Fact]
    public async Task CreateAsync_ZeroDuration_Fails()
    {
        var j = new TestJournal();
        (await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow(), minutes: 0))).IsSuccess.ShouldBeFalse();
        j.Store.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_SameTechniqueTwice_StoredOnce()
    {
        var j = new TestJournal();
        var t = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        var input = SessionBuilder.At(j.Time.GetUtcNow(), tech: t);
        input.Techniques.Add(new SessionTechniqueInput { TechniqueId = t, Focus = TechniqueFocus.Learned });
        var id = (await j.Sessions.CreateAsync(input)).Value;
        (await j.Sessions.GetAsync(id))!.Techniques.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetDefaultsAsync_NoSessions_UsesNowAndDefaults()
    {
        var j = new TestJournal();
        var d = await j.Sessions.GetDefaultsAsync();
        d.StartedAt.ShouldBe(j.Time.GetUtcNow());
        d.DurationMinutes.ShouldBe(90);
        d.Type.ShouldBe(SessionType.Class);
    }

    [Fact]
    public async Task GetDefaultsAsync_AfterSession_UsesLastDurationAndType()
    {
        var j = new TestJournal();
        await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow().AddDays(-1), minutes: 75, type: SessionType.OpenMat, attire: Attire.NoGi));
        var d = await j.Sessions.GetDefaultsAsync();
        d.DurationMinutes.ShouldBe(75);
        d.Type.ShouldBe(SessionType.OpenMat);
        d.Attire.ShouldBe(Attire.NoGi);
    }

    [Fact]
    public async Task GetAsync_RollsWithSubmissions_CountsForAndAgainst()
    {
        var j = new TestJournal();
        var input = SessionBuilder.At(j.Time.GetUtcNow());
        input.Rolls.Add(new RollInput
        {
            DurationMinutes = 6,
            Events =
            [
                new RollEventInput { Kind = RollEventKind.SubmissionFor },
                new RollEventInput { Kind = RollEventKind.SubmissionFor },
                new RollEventInput { Kind = RollEventKind.SubmissionAgainst },
                new RollEventInput { Kind = RollEventKind.SweepFor },
            ],
        });
        var id = (await j.Sessions.CreateAsync(input)).Value;
        var detail = (await j.Sessions.GetAsync(id))!;
        detail.SubmissionsFor.ShouldBe(2);
        detail.SubmissionsAgainst.ShouldBe(1);
    }

    [Fact]
    public async Task ListAsync_Filters_ApplyDateTypeAttireAndSatisfaction()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-20), type: SessionType.OpenMat, satisfaction: 5));
        await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-2), attire: Attire.NoGi, satisfaction: 2));
        await j.Sessions.CreateAsync(SessionBuilder.At(now, satisfaction: 4));

        (await j.Sessions.ListAsync(new SessionFilter(From: DateOnly.FromDateTime(now.AddDays(-7).Date)))).Count.ShouldBe(2);
        (await j.Sessions.ListAsync(new SessionFilter(Type: SessionType.OpenMat))).Count.ShouldBe(1);
        (await j.Sessions.ListAsync(new SessionFilter(Attire: Attire.NoGi))).Count.ShouldBe(1);
        (await j.Sessions.ListAsync(new SessionFilter(MinSatisfaction: 4))).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_Default_NewestFirstAndWithoutDeleted()
    {
        var j = new TestJournal();
        var now = j.Time.GetUtcNow();
        var old = (await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-3)))).Value;
        var recent = (await j.Sessions.CreateAsync(SessionBuilder.At(now))).Value;
        var deleted = (await j.Sessions.CreateAsync(SessionBuilder.At(now.AddDays(-1)))).Value;
        await j.Sessions.DeleteAsync(deleted);

        var list = await j.Sessions.ListAsync(new SessionFilter());
        list.Select(s => s.Id).ShouldBe([recent, old]);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_ReplacesData()
    {
        var j = new TestJournal();
        var id = (await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow()))).Value;
        var input = (await j.Sessions.GetInputAsync(id))!;
        input.DurationMinutes = 120;
        input.Notes = "Buen día";
        (await j.Sessions.UpdateAsync(id, input)).IsSuccess.ShouldBeTrue();
        var detail = (await j.Sessions.GetAsync(id))!;
        detail.DurationMinutes.ShouldBe(120);
        detail.Notes.ShouldBe("Buen día");
    }
}
