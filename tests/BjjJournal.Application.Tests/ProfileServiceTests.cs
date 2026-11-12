using BjjJournal.Application.Dtos;
using BjjJournal.Application.Seed;
using BjjJournal.Domain.Enums;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task IsOnboardedAsync_NewJournal_IsFalse() =>
        (await new TestJournal().Profiles.IsOnboardedAsync()).ShouldBeFalse();

    [Fact]
    public async Task CompleteOnboardingAsync_WithSeed_LoadsPositionsAndTechniques()
    {
        var j = new TestJournal();
        (await j.Profiles.CompleteOnboardingAsync(new ProfileInput { DisplayName = "Ana" }, loadSeed: true)).IsSuccess.ShouldBeTrue();
        (await j.Profiles.IsOnboardedAsync()).ShouldBeTrue();
        (await j.Positions.ListAsync()).Count.ShouldBe(SeedData.PositionCount);
        (await j.Techniques.SearchAsync(new TechniqueFilter())).Count.ShouldBe(SeedData.TechniqueCount);
    }

    [Fact]
    public async Task CompleteOnboardingAsync_WithoutSeed_LibraryIsEmpty()
    {
        var j = new TestJournal();
        await j.Profiles.CompleteOnboardingAsync(new ProfileInput { DisplayName = "Ana" }, loadSeed: false);
        (await j.Techniques.SearchAsync(new TechniqueFilter())).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_FiveStripes_Fails()
    {
        var j = new TestJournal();
        (await j.Profiles.SaveAsync(new ProfileInput { DisplayName = "Ana", Belt = Belt.Blue, Stripes = 5 })).IsSuccess.ShouldBeFalse();
        (await j.Profiles.IsOnboardedAsync()).ShouldBeFalse();
    }

    [Fact]
    public void SeedData_TechniquesReferenceExistingPositions_AndHaveNoDuplicates()
    {
        var data = new BjjJournal.Application.Persistence.AppData();
        SeedData.Apply(data, DateTimeOffset.UnixEpoch);
        var positionIds = data.Positions.Select(p => p.Id).ToHashSet();
        data.Techniques.ShouldAllBe(t => t.PositionId != null && positionIds.Contains(t.PositionId.Value));
        data.Techniques.Select(t => t.Name.ToLowerInvariant()).Distinct().Count().ShouldBe(data.Techniques.Count);
    }
}
