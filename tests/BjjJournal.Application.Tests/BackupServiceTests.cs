using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task ExportThenReplace_RoundTrip_KeepsAllData()
    {
        var source = new TestJournal();
        await source.Profiles.CompleteOnboardingAsync(new ProfileInput { DisplayName = "Guillem", Belt = Belt.Blue, Stripes = 2 }, loadSeed: true);
        var t = (await source.Techniques.SearchAsync(new TechniqueFilter("kimura")))[0].Id;
        await source.Sessions.CreateAsync(SessionBuilder.At(source.Time.GetUtcNow(), tech: t));
        var json = await source.Backup.ExportJsonAsync();

        var target = new TestJournal();
        (await target.Backup.ImportAsync(json, ImportMode.Replace)).IsSuccess.ShouldBeTrue();

        (await target.Profiles.GetAsync())!.Belt.ShouldBe(Belt.Blue);
        (await target.Techniques.SearchAsync(new TechniqueFilter())).Count.ShouldBe((await source.Techniques.SearchAsync(new TechniqueFilter())).Count);
        (await target.Sessions.ListAsync(new SessionFilter())).Single().TechniqueCount.ShouldBe(1);
    }

    [Fact]
    public async Task Export_ContainsSchemaVersion()
    {
        var json = await new TestJournal().Backup.ExportJsonAsync();
        json.ShouldContain("\"schemaVersion\": 1");
    }

    [Fact]
    public async Task PreviewAsync_InvalidJson_Fails()
    {
        (await new TestJournal().Backup.PreviewAsync("{ esto no es json")).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task PreviewAsync_FutureSchemaVersion_Fails()
    {
        (await new TestJournal().Backup.PreviewAsync("{\"schemaVersion\": 99}")).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task PreviewAsync_DoesNotChangeData()
    {
        var source = new TestJournal();
        await source.Sessions.CreateAsync(SessionBuilder.At(source.Time.GetUtcNow()));
        var json = await source.Backup.ExportJsonAsync();

        var target = new TestJournal();
        var preview = await target.Backup.PreviewAsync(json);
        preview.Value!.Sessions.ShouldBe(1);
        target.Store.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task Merge_DuplicateTechniqueByName_ReusesExistingAndRemapsSessions()
    {
        var source = new TestJournal();
        var srcKimura = (await source.Techniques.CreateAsync(new TechniqueInput { Name = "Kimura" })).Value;
        await source.Sessions.CreateAsync(SessionBuilder.At(source.Time.GetUtcNow(), tech: srcKimura));
        var json = await source.Backup.ExportJsonAsync();

        var target = new TestJournal();
        var tgtKimura = (await target.Techniques.CreateAsync(new TechniqueInput { Name = "kímura" })).Value;

        var preview = (await target.Backup.PreviewAsync(json)).Value!;
        preview.DuplicateTechniques.ShouldBe(1);

        (await target.Backup.ImportAsync(json, ImportMode.Merge)).IsSuccess.ShouldBeTrue();
        (await target.Techniques.SearchAsync(new TechniqueFilter())).Count.ShouldBe(1);
        (await target.Techniques.GetAsync(tgtKimura))!.TimesWorked.ShouldBe(1);
    }

    [Fact]
    public async Task Merge_SameFileTwice_DoesNotDuplicateSessions()
    {
        var source = new TestJournal();
        await source.Sessions.CreateAsync(SessionBuilder.At(source.Time.GetUtcNow()));
        var json = await source.Backup.ExportJsonAsync();

        var target = new TestJournal();
        await target.Backup.ImportAsync(json, ImportMode.Merge);
        (await target.Backup.PreviewAsync(json)).Value!.DuplicateSessions.ShouldBe(1);
        await target.Backup.ImportAsync(json, ImportMode.Merge);
        (await target.Sessions.ListAsync(new SessionFilter())).Count.ShouldBe(1);
    }
}
