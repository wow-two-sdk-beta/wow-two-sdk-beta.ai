using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Unit.Evals;

public sealed class EvalReportMathTests
{
    [Theory]
    [InlineData(50, 30)]
    [InlineData(95, 100)]
    [InlineData(100, 100)]
    [InlineData(1, 10)]
    public void Percentile_ShouldTakeTheNearestRank(int percent, long expected)
    {
        EvalReportMath.Percentile([10, 20, 30, 40, 100], percent).Should().Be(expected);
    }

    [Fact]
    public void Percentile_ShouldBeZero_ForNoValues()
    {
        EvalReportMath.Percentile([], 50).Should().Be(0);
    }

    [Fact]
    public void Summarize_ShouldCountOutcomesCostsLatencyRatingsAndTags()
    {
        EvalItemResult[] items =
        [
            Item(AiOutcome.Succeeded, correct: true, cost: 100, latency: 10, tags: ["lang:ru"], rating: 5),
            Item(AiOutcome.Succeeded, correct: false, severe: true, cost: 100, latency: 30, tags: ["lang:ru", "slice:positive"], fromCache: true),
            Item(AiOutcome.SchemaInvalid, cost: 50, latency: 20, tags: ["lang:uz"], rating: 2),
            Item(AiOutcome.Failed, latency: 0),
        ];

        var summary = EvalReportMath.Summarize(Run(), items, (1000, 200, 300));

        summary.Items.Should().Be(4);
        summary.Answered.Should().Be(2);
        summary.Correct.Should().Be(1);
        summary.Accuracy.Should().Be(0.25);
        summary.SevereMisses.Should().Be(1);
        summary.SchemaInvalid.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.FromCache.Should().Be(1);
        summary.CostMicroUsd.Should().Be(250);
        summary.CostPerItemMicroUsd.Should().Be(63);
        summary.P50LatencyMs.Should().Be(10);
        summary.P95LatencyMs.Should().Be(30);
        summary.MeanRating.Should().Be(3.5);
        summary.InputTokens.Should().Be(1000);
        summary.CacheReadTokens.Should().Be(300);
        summary.AccuracyByTag.Should().BeEquivalentTo(new Dictionary<string, double> { ["lang:ru"] = 0.5, ["lang:uz"] = 0, ["slice:positive"] = 0 });
    }

    private static EvalRunModel Run() => new()
    {
        Id = Guid.NewGuid(),
        Set = "afisha-10",
        UseCase = "relevance",
        PromptVersion = 1,
        PromptHash = "abcdefabcdef",
        Provider = "anthropic",
        Model = "claude-haiku-5-5",
        StartedAtUtc = DateTimeOffset.UnixEpoch,
    };

    private static EvalItemResult Item(AiOutcome outcome, bool correct = false, bool severe = false, long cost = 0, long latency = 0,
        string[]? tags = null, int? rating = null, bool fromCache = false) => new()
    {
        RunId = Guid.Empty,
        ItemId = Guid.NewGuid(),
        Expected = "{}",
        Outcome = outcome,
        Correct = correct,
        Severe = severe,
        CostMicroUsd = cost,
        LatencyMs = latency,
        Tags = tags ?? [],
        Rating = rating,
        FromCache = fromCache,
    };
}
