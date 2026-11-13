using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Sums one run's item results into its summary.</summary>
internal static class EvalReportMath
{
    /// <summary>Summarizes a run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="items">Its item results.</param>
    /// <param name="tokens">Its tokens: input, output and cached input.</param>
    public static EvalRunSummary Summarize(EvalRunModel run, IReadOnlyList<EvalItemResult> items, (long Input, long Output, long CacheRead) tokens)
    {
        var count = items.Count;
        var correct = items.Count(item => item.Correct);
        var cost = items.Sum(item => item.CostMicroUsd);
        var latencies = items.Select(item => item.LatencyMs).Order().ToArray();
        var ratings = items.Where(item => item.Rating is not null).Select(item => (double)item.Rating!.Value).ToArray();
        var byTag = items
            .SelectMany(item => item.Tags.Distinct(StringComparer.Ordinal).Select(tag => (Tag: tag, item.Correct)))
            .GroupBy(pair => pair.Tag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Ratio(group.Count(pair => pair.Correct), group.Count()), StringComparer.Ordinal);

        return new EvalRunSummary
        {
            Run = run,
            Items = count,
            Answered = items.Count(item => item.Outcome == AiOutcome.Succeeded),
            Correct = correct,
            Accuracy = Ratio(correct, count),
            SevereMisses = items.Count(item => item.Severe),
            SchemaInvalid = items.Count(item => item.Outcome == AiOutcome.SchemaInvalid),
            Failed = items.Count(item => item.Outcome is AiOutcome.Failed or AiOutcome.Refused),
            FromCache = items.Count(item => item.FromCache),
            CostMicroUsd = cost,
            CostPerItemMicroUsd = count == 0 ? 0 : (long)Math.Round((double)cost / count, MidpointRounding.AwayFromZero),
            InputTokens = tokens.Input,
            OutputTokens = tokens.Output,
            CacheReadTokens = tokens.CacheRead,
            P50LatencyMs = Percentile(latencies, 50),
            P95LatencyMs = Percentile(latencies, 95),
            MeanRating = ratings.Length == 0 ? null : Math.Round(ratings.Average(), 2),
            AccuracyByTag = byTag,
        };
    }

    /// <summary>Gets the nearest-rank percentile of sorted values; 0 for none.</summary>
    /// <param name="sorted">The values, ascending.</param>
    /// <param name="percent">The percentile, 1 to 100.</param>
    public static long Percentile(IReadOnlyList<long> sorted, int percent)
    {
        if (sorted.Count == 0)
            return 0;

        var rank = (int)Math.Ceiling(percent / 100.0 * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }

    private static double Ratio(int part, int whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 4);
}
