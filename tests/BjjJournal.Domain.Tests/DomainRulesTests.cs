using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;
using BjjJournal.Domain.Rules;
using Shouldly;

namespace BjjJournal.Domain.Tests;

public class DomainRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void IsValidSatisfaction_Value_ReturnsExpected(int value, bool expected) =>
        DomainRules.IsValidSatisfaction(value).ShouldBe(expected);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void IsValidStripes_Value_ReturnsExpected(int value, bool expected) =>
        DomainRules.IsValidStripes(value).ShouldBe(expected);

    [Theory]
    [InlineData(0, false)]
    [InlineData(-10, false)]
    [InlineData(1, true)]
    public void IsValidDuration_Minutes_ReturnsExpected(int minutes, bool expected) =>
        DomainRules.IsValidDuration(minutes).ShouldBe(expected);

    [Fact]
    public void ValidateSession_ZeroDuration_Fails() =>
        DomainRules.Validate(new TrainingSession { DurationMinutes = 0, Satisfaction = 3 }).IsSuccess.ShouldBeFalse();

    [Fact]
    public void ValidateSession_SatisfactionOutOfRange_Fails() =>
        DomainRules.Validate(new TrainingSession { DurationMinutes = 60, Satisfaction = 6 }).IsSuccess.ShouldBeFalse();

    [Fact]
    public void ValidateSession_RollWithZeroMinutes_Fails()
    {
        var session = new TrainingSession { DurationMinutes = 60, Satisfaction = 3, Rolls = [new Roll { DurationMinutes = 0 }] };
        DomainRules.Validate(session).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSession_ValidValues_Succeeds() =>
        DomainRules.Validate(new TrainingSession { DurationMinutes = 90, Satisfaction = 4 }).IsSuccess.ShouldBeTrue();

    [Fact]
    public void ValidateProfile_EmptyName_Fails() =>
        DomainRules.Validate(new UserProfile { DisplayName = " ", Stripes = 0 }).IsSuccess.ShouldBeFalse();

    [Fact]
    public void ValidateProfile_FiveStripes_Fails() =>
        DomainRules.Validate(new UserProfile { DisplayName = "Ana", Stripes = 5 }).IsSuccess.ShouldBeFalse();

    [Fact]
    public void ValidateTechnique_EmptyName_Fails() =>
        DomainRules.Validate(new Technique { Name = "" }).IsSuccess.ShouldBeFalse();

    [Fact]
    public void CanAddLink_SameTechnique_Fails()
    {
        var id = Guid.NewGuid();
        DomainRules.CanAddLink(new TechniqueLink { FromTechniqueId = id, ToTechniqueId = id }, []).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void CanAddLink_DuplicatePairAndKind_Fails()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var existing = new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b, Kind = LinkKind.ChainsTo };
        var link = new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b, Kind = LinkKind.ChainsTo };
        DomainRules.CanAddLink(link, [existing]).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void CanAddLink_SamePairDifferentKind_Succeeds()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var existing = new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b, Kind = LinkKind.ChainsTo };
        var link = new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b, Kind = LinkKind.CounterTo };
        DomainRules.CanAddLink(link, [existing]).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CanAddLink_DuplicateOfDeletedLink_Succeeds()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var existing = new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b, IsDeleted = true };
        DomainRules.CanAddLink(new TechniqueLink { FromTechniqueId = a, ToTechniqueId = b }, [existing]).IsSuccess.ShouldBeTrue();
    }
}
