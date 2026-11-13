using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;

namespace Ai.Tests.Unit.Runs;

public sealed class OutputSchemaCheckTests
{
    private const string Schema = """
        {"type":"object","required":["relevance","kind"],
         "properties":{"relevance":{"type":"string","enum":["close","far","none"]},"kind":{"type":["string","null"]},
                       "score":{"type":"integer"},"confident":{"type":"boolean"},"tags":{"type":"array"}}}
        """;

    [Theory]
    [InlineData("""{"relevance":"close","kind":"runway"}""")]
    [InlineData(" ```json\n{\"relevance\":\"far\",\"kind\":null}\n``` ")]
    [InlineData("```\n{\"relevance\":\"none\",\"kind\":\"x\",\"score\":3,\"confident\":true,\"tags\":[]}\n```")]
    [InlineData("""{"relevance":"close","kind":"x","score":3.0,"extra":{"nested":1}}""")]
    public void TryCheck_ShouldPass_AValidAnswer(string answer)
    {
        OutputSchemaCheck.TryCheck(answer, Schema, out var json, out var error).Should().BeTrue(error);

        json.Should().StartWith("{").And.EndWith("}");
    }

    [Theory]
    [InlineData("not json", "not JSON")]
    [InlineData("""["close"]""", "not an object")]
    [InlineData("""{"relevance":"close"}""", "kind")]
    [InlineData("""{"relevance":"maybe","kind":"x"}""", "not one of")]
    [InlineData("""{"relevance":"close","kind":3}""", "'kind'")]
    [InlineData("""{"relevance":"close","kind":"x","score":2.5}""", "'score'")]
    [InlineData("""{"relevance":"close","kind":"x","confident":"yes"}""", "'confident'")]
    public void TryCheck_ShouldFail_AnAnswerThatMissesTheSchema(string answer, string reason)
    {
        OutputSchemaCheck.TryCheck(answer, Schema, out _, out var error).Should().BeFalse();

        error.Should().Contain(reason);
    }

    [Fact]
    public void StripFence_ShouldKeepAnUnfencedAnswer()
    {
        OutputSchemaCheck.StripFence("  {\"a\":1}\n").Should().Be("{\"a\":1}");
        OutputSchemaCheck.StripFence("```json\n{\"a\":1}\n```").Should().Be("{\"a\":1}");
    }
}
