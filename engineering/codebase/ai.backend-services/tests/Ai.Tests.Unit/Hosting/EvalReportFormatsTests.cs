using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Reports;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Unit.Hosting;

public sealed class EvalReportFormatsTests
{
    private static readonly Guid RunId = Guid.Parse("0192f0aa-0000-7000-8000-000000000001");

    private static EvalReportModel Report() => new()
    {
        Set = "afisha-2",
        UseCase = "relevance",
        Runs =
        [
            new EvalRunSummary
            {
                Run = new EvalRunModel
                {
                    Id = RunId, Set = "afisha-2", UseCase = "relevance", PromptVersion = 2, PromptHash = "abcdef012345", Provider = "anthropic",
                    Model = "claude-haiku-5-5", StartedAtUtc = DateTimeOffset.UnixEpoch, Status = EvalRunStatus.Canceled,
                },
                Items = 2, CostMicroUsd = 300, SpentMicroUsd = 100, Unpriced = 1, FromCache = 1,
            },
        ],
        Items =
        [
            new EvalItemResult { RunId = RunId, ItemId = Guid.NewGuid(), Expected = """{"relevance":"close"}""", Outcome = AiOutcome.Succeeded, Correct = true, Priced = true, PromptHash = "abcdef012345", Subject = "post, with comma" },
            new EvalItemResult { RunId = RunId, ItemId = Guid.NewGuid(), Expected = """{"relevance":"none"}""", Outcome = AiOutcome.SchemaInvalid, Misses = ["relevance:none>close"] },
        ],
    };

    [Fact]
    public void Csv_ShouldNameTheStatusAndPinnedHash_AndQuoteFields()
    {
        var lines = EvalReportFormats.Csv(Report()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToList();

        lines.Should().HaveCount(3);
        lines[0].Should().Be("run_id,status,provider,model,prompt_version,prompt_hash,item_id,subject,tags,outcome,correct,severe,misses,cost_micro_usd,priced,latency_ms,from_cache,rating,comment,expected,answer");
        lines[1].Should().Contain(",canceled,anthropic,claude-haiku-5-5,2,abcdef012345,").And.Contain("\"post, with comma\"").And.Contain(",succeeded,true,");
        lines[2].Should().Contain(",schemaInvalid,false,").And.Contain(",0,false,");
    }

    [Fact]
    public void Markdown_ShouldShowSpendApartFromCost_AndTheUnpricedItems()
    {
        var text = EvalReportFormats.Markdown(Report());

        text.Should().Contain("| Run | Status | Model | Prompt |").And.Contain("| canceled | anthropic/claude-haiku-5-5 | v2 `abcdef012345` |");
        text.Should().Contain("| 1 | 1 | $0 | $0.0003 | $0.0001 |", "cached 1, unpriced 1, cost per item, cost, spent");
        text.Should().Contain("relevance:none>close");
        EvalReportFormats.Usd(AiConstants.MicroUsdPerUsd).Should().Be("$1");
    }
}
