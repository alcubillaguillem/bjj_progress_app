using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class TechniqueServiceTests
{
    [Fact]
    public async Task CreateAsync_NameAndCategory_Succeeds()
    {
        var j = new TestJournal();
        var result = await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura", Category = TechniqueCategory.Submission });
        result.IsSuccess.ShouldBeTrue();
        (await j.Techniques.GetAsync(result.Value))!.Name.ShouldBe("Kimura");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_Fails()
    {
        var j = new TestJournal();
        (await j.Techniques.CreateAsync(new TechniqueInput { Name = "  " })).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_SameNameAndPositionIgnoringAccents_Fails()
    {
        var j = new TestJournal();
        var pos = (await j.Positions.CreateAsync("Guardia cerrada", PositionRole.Bottom)).Value!.Id;
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Triángulo", PositionId = pos });
        var dup = await j.Techniques.CreateAsync(new TechniqueInput { Name = "TRIANGULO", PositionId = pos });
        dup.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_SameNameDifferentPosition_Succeeds()
    {
        var j = new TestJournal();
        var a = (await j.Positions.CreateAsync("Guardia cerrada", PositionRole.Bottom)).Value!.Id;
        var b = (await j.Positions.CreateAsync("Montada", PositionRole.Top)).Value!.Id;
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Armbar", PositionId = a });
        (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Armbar", PositionId = b })).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_SameNameAsDeletedTechnique_Succeeds()
    {
        var j = new TestJournal();
        var first = await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" });
        await j.Techniques.DeleteAsync(first.Value);
        (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAsync_InvalidInput_DoesNotChangeTechnique()
    {
        var j = new TestJournal();
        var id = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        (await j.Techniques.UpdateAsync(id, new TechniqueInput { Name = "" })).IsSuccess.ShouldBeFalse();
        (await j.Techniques.GetAsync(id))!.Name.ShouldBe("Kimura");
    }

    [Fact]
    public async Task SearchAsync_ByTagNotesOrPosition_FindsTechnique()
    {
        var j = new TestJournal();
        var pos = (await j.Positions.CreateAsync("Media guardia", PositionRole.Bottom)).Value!.Id;
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Old school", PositionId = pos, Tags = ["#favorita"], Notes = "Agarrar el tobillo" });
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Otra" });

        (await j.Techniques.SearchAsync(new TechniqueFilter("favorita"))).Count.ShouldBe(1);
        (await j.Techniques.SearchAsync(new TechniqueFilter("tobillo"))).Count.ShouldBe(1);
        (await j.Techniques.SearchAsync(new TechniqueFilter("MEDIA"))).Count.ShouldBe(1);
        (await j.Techniques.SearchAsync(new TechniqueFilter(Tag: "favorita"))).Single().Name.ShouldBe("Old school");
    }

    [Fact]
    public async Task SearchAsync_ByCategory_FiltersOthers()
    {
        var j = new TestJournal();
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura", Category = TechniqueCategory.Submission });
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "Tijera", Category = TechniqueCategory.Sweep });
        (await j.Techniques.SearchAsync(new TechniqueFilter(Category: TechniqueCategory.Sweep))).Single().Name.ShouldBe("Tijera");
    }

    [Fact]
    public async Task GetAsync_WorkedInSessions_ShowsTimesAndLastDate()
    {
        var j = new TestJournal();
        var id = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        var older = j.Time.GetUtcNow().AddDays(-5);
        var newer = j.Time.GetUtcNow().AddDays(-1);
        await j.Sessions.CreateAsync(SessionBuilder.At(older, tech: id));
        await j.Sessions.CreateAsync(SessionBuilder.At(newer, tech: id));
        await j.Sessions.CreateAsync(SessionBuilder.At(newer));

        var detail = (await j.Techniques.GetAsync(id))!;
        detail.TimesWorked.ShouldBe(2);
        detail.LastWorked.ShouldBe(newer);
    }

    [Fact]
    public async Task DeleteAsync_TechniqueInSession_KeepsHistory()
    {
        var j = new TestJournal();
        var id = (await j.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        var sessionId = (await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow(), tech: id))).Value;
        await j.Techniques.DeleteAsync(id);

        (await j.Techniques.SearchAsync(new TechniqueFilter())).ShouldBeEmpty();
        (await j.Sessions.GetAsync(sessionId))!.Techniques.Single().Name.ShouldBe("Kimura");
    }

    [Fact]
    public async Task GetBeltProgressAsync_MixedProgress_CountsCompetentAndMastered()
    {
        var j = new TestJournal();
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "A", CurriculumBelt = Belt.White, Progress = ProgressStatus.Competent });
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "B", CurriculumBelt = Belt.White, Progress = ProgressStatus.Mastered });
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "C", CurriculumBelt = Belt.White, Progress = ProgressStatus.Learning });
        await j.Techniques.CreateAsync(new TechniqueInput { Name = "D", CurriculumBelt = Belt.White });

        var white = (await j.Techniques.GetBeltProgressAsync()).Single();
        white.Total.ShouldBe(4);
        white.CompetentOrMastered.ShouldBe(2);
        white.Percent.ShouldBe(50);
    }
}
