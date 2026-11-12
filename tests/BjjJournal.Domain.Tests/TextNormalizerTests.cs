using BjjJournal.Domain.Rules;
using Shouldly;

namespace BjjJournal.Domain.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Triángulo", "triangulo")]
    [InlineData("  KÍMURA ", "kimura")]
    [InlineData("Mata León", "mata leon")]
    [InlineData(null, "")]
    public void Normalize_Text_RemovesAccentsAndCase(string? input, string expected) =>
        TextNormalizer.Normalize(input).ShouldBe(expected);

    [Fact]
    public void Contains_AccentInsensitiveQuery_Matches() =>
        TextNormalizer.Contains("Guardia de La Riva invertida", TextNormalizer.Normalize("RIVA")).ShouldBeTrue();

    [Fact]
    public void Contains_EmptyQuery_AlwaysMatches() =>
        TextNormalizer.Contains(null, "").ShouldBeTrue();
}
