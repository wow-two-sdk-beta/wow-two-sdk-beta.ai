using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace Ai.Tests.Unit.Prompts;

public sealed class ModelMatchTests
{
    [Theory]
    [InlineData(null, "claude-haiku-5-5", ModelMatch.Generic)]
    [InlineData("claude-haiku-5-5", "claude-haiku-5-5", ModelMatch.Exact)]
    [InlineData("CLAUDE-HAIKU-5-5", "claude-haiku-5-5", ModelMatch.Exact)]
    [InlineData("claude-*", "claude-haiku-5-5", 8)]
    [InlineData("claude-haiku-*", "claude-haiku-5-5", 14)]
    [InlineData("claude-*", "gpt-6-luna", ModelMatch.NoFit)]
    [InlineData("gpt-6-luna", "gpt-6-luna-mini", ModelMatch.NoFit)]
    public void Specificity_ShouldRankExactAboveLongerPrefixAboveGeneric(string? match, string model, int expected)
    {
        ModelMatch.Specificity(match, model).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("claude-*", true)]
    [InlineData("gpt-6-luna", true)]
    [InlineData("*", false)]
    [InlineData("*claude", false)]
    [InlineData("cla*ude", false)]
    [InlineData("claude haiku", false)]
    public void IsValid_ShouldAcceptAnIdOrATrailingStarPrefix(string? match, bool valid)
    {
        ModelMatch.IsValid(match).Should().Be(valid);
    }

    [Fact]
    public void Pick_ShouldPreferSpecificity_ThenVersion_AtOrBelowTheTarget()
    {
        PromptModel[] rows =
        [
            Row(1, "claude-haiku-5-5"),
            Row(2, "claude-*"),
            Row(3, null),
            Row(4, "claude-*"),
            Row(5, null),
        ];

        ModelMatch.Pick(rows, "claude-haiku-5-5", 5)!.Version.Should().Be(1, "an exact match beats a newer prefix or generic row");
        ModelMatch.Pick(rows, "claude-sonnet-5-5", 5)!.Version.Should().Be(4, "among equal matches the highest version wins");
        ModelMatch.Pick(rows, "claude-sonnet-5-5", 3)!.Version.Should().Be(2, "rows above the target never serve");
        ModelMatch.Pick(rows, "gpt-6-luna", 4)!.Version.Should().Be(3);
        ModelMatch.Pick(rows, "gpt-6-luna", 0).Should().BeNull();
        ModelMatch.Pick([Row(1, "claude-*")], "gpt-6-luna", 1).Should().BeNull();
    }

    private static PromptModel Row(int version, string? match) => new()
    {
        Id = Guid.NewGuid(),
        UseCase = "relevance",
        ModelMatch = match,
        Version = version,
        SystemTemplate = "s",
        UserTemplate = $"v{version}",
        ContentHash = "000000000000",
    };
}
