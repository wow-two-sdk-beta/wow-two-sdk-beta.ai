using System.Globalization;
using System.Text;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;

namespace Ai.Api.Operator;

/// <summary>Writes an eval report as a Markdown table for chat or as CSV for a spreadsheet.</summary>
public static class EvalReportFormats
{
    /// <summary>Writes the report as Markdown: one row per run (prompt version × model with accuracy, misses, cost and
    /// latency), then every item a run did not get right.</summary>
    /// <param name="report">The report.</param>
    public static string Markdown(EvalReportModel report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"### Eval `{report.Set}` · use case `{report.UseCase}`");
        text.AppendLine();
        text.AppendLine("| Run | Model | Prompt | Items | Accuracy | Severe | Schema-invalid | Failed | Cached | Cost/item | Cost | p50 ms | p95 ms | Tokens in/out | Rating |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var run in report.Runs)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| `{Short(run.Run.Id)}` | {run.Run.Provider}/{run.Run.Model} | v{run.Run.PromptVersion} `{run.Run.PromptHash}` | {run.Items} | " +
                $"{run.Accuracy:P1} | {run.SevereMisses} | {run.SchemaInvalid} | {run.Failed} | {run.FromCache} | {Usd(run.CostPerItemMicroUsd)} | " +
                $"{Usd(run.CostMicroUsd)} | {run.P50LatencyMs} | {run.P95LatencyMs} | {run.InputTokens}/{run.OutputTokens} | " +
                $"{(run.MeanRating is { } rating ? rating.ToString("0.##", CultureInfo.InvariantCulture) : "–")} |");
        }

        var misses = report.Items.Where(item => !item.Correct).ToList();
        if (misses.Count == 0)
            return text.ToString();

        var runs = report.Runs.ToDictionary(run => run.Run.Id);
        text.AppendLine();
        text.AppendLine("| Run | Item | Subject | Outcome | Misses | Severe |");
        text.AppendLine("|---|---|---|---|---|---|");
        foreach (var item in misses)
        {
            var model = runs.TryGetValue(item.RunId, out var run) ? $"{run.Run.Model} v{run.Run.PromptVersion}" : Short(item.RunId);
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {model} | `{Short(item.ItemId)}` | {Cell(item.Subject ?? "–")} | {JsonNamingPolicy.CamelCase.ConvertName(item.Outcome.ToString())} | " +
                $"{Cell(item.Misses.Count == 0 ? "–" : string.Join("; ", item.Misses))} | {(item.Severe ? "yes" : "")} |");
        }

        return text.ToString();
    }

    /// <summary>Writes the report as CSV: one row per run and item, with the run's prompt and model.</summary>
    /// <param name="report">The report.</param>
    public static string Csv(EvalReportModel report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var runs = report.Runs.ToDictionary(run => run.Run.Id);
        var text = new StringBuilder();
        text.AppendLine("run_id,provider,model,prompt_version,prompt_hash,item_id,subject,tags,outcome,correct,severe,misses,cost_micro_usd,latency_ms,from_cache,rating,comment,expected,answer");
        foreach (var item in report.Items)
        {
            var run = runs[item.RunId].Run;
            text.AppendLine(string.Join(',', new[]
            {
                item.RunId.ToString(), run.Provider, run.Model, run.PromptVersion.ToString(CultureInfo.InvariantCulture), run.PromptHash,
                item.ItemId.ToString(), item.Subject ?? string.Empty, string.Join(';', item.Tags),
                JsonNamingPolicy.CamelCase.ConvertName(item.Outcome.ToString()), Bool(item.Correct), Bool(item.Severe), string.Join("; ", item.Misses),
                item.CostMicroUsd.ToString(CultureInfo.InvariantCulture), item.LatencyMs.ToString(CultureInfo.InvariantCulture), Bool(item.FromCache),
                item.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, item.Comment ?? string.Empty, item.Expected, item.Answer ?? string.Empty,
            }.Select(Field)));
        }

        return text.ToString();
    }

    /// <summary>Writes micro-USD as dollars.</summary>
    /// <param name="microUsd">The amount in micro-USD.</param>
    public static string Usd(long microUsd) =>
        "$" + ((decimal)microUsd / AiConstants.MicroUsdPerUsd).ToString("0.######", CultureInfo.InvariantCulture);

    private static string Short(Guid id) => id.ToString("N")[^8..];

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace('\n', ' ');

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
