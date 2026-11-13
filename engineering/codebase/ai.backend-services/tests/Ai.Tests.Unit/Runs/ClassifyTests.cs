using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Unit.Runs;

public sealed class ClassifyTests
{
    private const string Schema = """{"type":"object","required":["relevance"]}""";

    [Fact]
    public void Classify_ShouldSucceed_WithTheFenceStrippedAnswer()
    {
        var (outcome, kind, _, answer) = ModuleAiRunner.Classify(Result("```json\n{\"relevance\":\"close\"}\n```"), Schema);

        outcome.Should().Be(AiOutcome.Succeeded);
        kind.Should().BeNull();
        answer.Should().Be("{\"relevance\":\"close\"}");
    }

    [Fact]
    public void Classify_ShouldKeepFreeText_WithNoSchema()
    {
        ModuleAiRunner.Classify(Result("  a summary  "), null).Answer.Should().Be("  a summary  ");
    }

    [Theory]
    [InlineData(AiErrorKind.ContentFiltered, AiOutcome.Refused)]
    [InlineData(AiErrorKind.SchemaMismatch, AiOutcome.SchemaInvalid)]
    [InlineData(AiErrorKind.RateLimited, AiOutcome.Failed)]
    [InlineData(AiErrorKind.NotConfigured, AiOutcome.Failed)]
    public void Classify_ShouldMapTheProvidersErrorKind(AiErrorKind kind, AiOutcome expected)
    {
        var result = Result(null) with { ErrorKind = kind, Error = "boom" };

        ModuleAiRunner.Classify(result, Schema).Should().Be((expected, kind, "boom", (string?)null));
    }

    [Fact]
    public void Classify_ShouldRefuse_ARefusalFinishReason_AndFail_AnEmptyAnswer()
    {
        ModuleAiRunner.Classify(Result("I can't help", "refusal"), null).Outcome.Should().Be(AiOutcome.Refused);
        ModuleAiRunner.Classify(Result(null), null).Should().Match<(AiOutcome Outcome, AiErrorKind? Kind, string? Error, string? Answer)>(
            classified => classified.Outcome == AiOutcome.Failed && classified.Kind == AiErrorKind.InvalidResponse);
    }

    [Fact]
    public void Classify_ShouldMarkSchemaInvalid_AnAnswerThatMissesTheSchema()
    {
        var (outcome, kind, error, answer) = ModuleAiRunner.Classify(Result("""{"kind":"x"}"""), Schema);

        outcome.Should().Be(AiOutcome.SchemaInvalid);
        kind.Should().Be(AiErrorKind.SchemaMismatch);
        error.Should().Contain("relevance");
        answer.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1, 2000)]
    [InlineData(2, 4000)]
    public void Backoff_ShouldDoubleTheDelay_PerRetry(int retry, int milliseconds)
    {
        ModuleAiRunner.Backoff(TimeSpan.FromSeconds(1), retry).Should().Be(TimeSpan.FromMilliseconds(milliseconds));
        ModuleAiRunner.Backoff(TimeSpan.Zero, retry).Should().Be(TimeSpan.Zero);
    }

    private static AiCallResult Result(string? text, string finishReason = "end_turn") => new()
    {
        Model = new AiModelRef("anthropic", "claude-haiku-5-5"),
        StartedAtUtc = DateTimeOffset.UnixEpoch,
        LatencyMs = 10,
        Text = text,
        FinishReason = finishReason,
    };
}
