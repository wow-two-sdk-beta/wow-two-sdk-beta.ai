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
    public void TryCheck_ShouldLeaveAnOutOfListValue_ToACallerThatRepairsIt()
    {
        const string answer = """{"relevance":"maybe","kind":"x"}""";

        OutputSchemaCheck.TryCheck(answer, Schema, out var json, out var error, checkEnums: false).Should().BeTrue(error);
        json.Should().Be(answer);
        OutputSchemaCheck.TryCheck("""{"relevance":"maybe","kind":3}""", Schema, out _, out error, checkEnums: false)
            .Should().BeFalse("a wrong type still fails");
        error.Should().Contain("'kind'");
        OutputSchemaCheck.TryCheck("""{"relevance":"maybe"}""", Schema, out _, out _, checkEnums: false)
            .Should().BeFalse("a missing required property still fails");
    }

    [Theory]
    [InlineData("Here is the answer:\n```json\n{\"relevance\":\"far\",\"kind\":null}\n```\nHope it helps.")]
    [InlineData("```json\n{\"relevance\":\"far\",\"kind\":null}\n```\nThe post is about a concert.")]
    [InlineData("The post {mentions braces}. Answer: {\"relevance\":\"far\",\"kind\":null}")]
    [InlineData("{\"relevance\":\"far\",\"kind\":null}\n\nNote: the date {is} unclear.")]
    [InlineData("Answer:\n\n{\n  \"relevance\": \"far\",\n  \"kind\": null\n}")]
    public void TryCheck_ShouldPass_AnObjectWrappedInProse(string answer)
    {
        OutputSchemaCheck.TryCheck(answer, Schema, out var json, out var error).Should().BeTrue(error);

        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("relevance").GetString().Should().Be("far");
    }

    [Theory]
    [InlineData("Here it is: {\"relevance\":\"maybe\",\"kind\":\"x\"}", "not one of")]
    [InlineData("I think it is far.", "not JSON")]
    [InlineData("""[{"relevance":"close","kind":"x"}]""", "not an object")]
    [InlineData("Answer: {\"relevance\":\"far\", \"kind\": ", "not JSON")]
    public void TryCheck_ShouldStillFail_AProseAnswerThatMissesTheSchema(string answer, string reason)
    {
        OutputSchemaCheck.TryCheck(answer, Schema, out _, out var error).Should().BeFalse();

        error.Should().Contain(reason);
    }

    [Fact]
    public void ExtractJson_ShouldKeepAWholeAnswerThatParses()
    {
        OutputSchemaCheck.ExtractJson(" {\"a\":{\"b\":1}} ").Should().Be("{\"a\":{\"b\":1}}");
        OutputSchemaCheck.ExtractJson("Answer: {\"a\":\"ünïcode {x}\"} done").Should().Be("{\"a\":\"ünïcode {x}\"}");
    }

    [Fact]
    public void StripFence_ShouldKeepAnUnfencedAnswer()
    {
        OutputSchemaCheck.StripFence("  {\"a\":1}\n").Should().Be("{\"a\":1}");
        OutputSchemaCheck.StripFence("```json\n{\"a\":1}\n```").Should().Be("{\"a\":1}");
    }
}
