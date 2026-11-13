using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace Ai.Tests.Unit.Shared;

public sealed class AiHashesTests
{
    [Fact]
    public void Content_ShouldBeTwelveLowercaseHex_AndDeterministic()
    {
        var parameters = new PromptParameters { Temperature = 0.2, MaxOutputTokens = 256, Effort = "low" };

        var first = AiHashes.Content("system", "user {{x}}", parameters);
        var second = AiHashes.Content("system", "user {{x}}", parameters with { });

        first.Should().MatchRegex("^[0-9a-f]{12}$").And.Be(second);
        first.Should().Be(AiHashes.Sha256("system\u001fuser {{x}}\u001f{\"effort\":\"low\",\"maxOutputTokens\":256,\"temperature\":0.2}")[..12]);
    }

    [Fact]
    public void Content_ShouldChange_WhenATemplateOrAParameterChanges()
    {
        var baseline = AiHashes.Content("system", "user", new PromptParameters());

        AiHashes.Content("system", "user ", new PromptParameters()).Should().NotBe(baseline);
        AiHashes.Content("system", "user", new PromptParameters { Temperature = 0 }).Should().NotBe(baseline);
        AiHashes.Content("systemuser", "", new PromptParameters()).Should().NotBe(AiHashes.Content("system", "user", new PromptParameters()),
            "the separator keeps text moved between the templates apart");
    }

    [Fact]
    public void CanonicalParameters_ShouldOrderKeys_AndLeaveNullsOut()
    {
        AiJson.CanonicalParameters(new PromptParameters()).Should().Be("{}");
        AiJson.CanonicalParameters(new PromptParameters { Temperature = 1, MaxOutputTokens = 10 }).Should().Be("""{"maxOutputTokens":10,"temperature":1}""");
    }

    [Fact]
    public void Input_ShouldCoverTheRenderedMessagesSchemaModelAndParameters()
    {
        var parameters = new PromptParameters { MaxOutputTokens = 1024 };
        var baseline = AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters);

        baseline.Should().MatchRegex("^[0-9a-f]{64}$");
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters).Should().Be(baseline);
        AiHashes.Input("s", "u2", "{}", "anthropic/claude-haiku-5-5", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", null, "anthropic/claude-haiku-5-5", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", "{}", "openai/gpt-6-luna", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters with { MaxOutputTokens = 512 }).Should().NotBe(baseline);
    }
}
