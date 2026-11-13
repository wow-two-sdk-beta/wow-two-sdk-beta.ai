namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an eval set's report: runs joined with their scores, inferences and prices.</summary>
public sealed record EvalReportModel
{
    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets one summary per run, oldest first.</summary>
    public required IReadOnlyList<EvalRunSummary> Runs { get; init; }

    /// <summary>Gets one row per run and item.</summary>
    public required IReadOnlyList<EvalItemResult> Items { get; init; }
}

/// <summary>Represents one run's totals.</summary>
public sealed record EvalRunSummary
{
    /// <summary>Gets the run.</summary>
    public required EvalRunModel Run { get; init; }

    /// <summary>Gets the items in the set when the run ran.</summary>
    public int Items { get; init; }

    /// <summary>Gets the items that got a schema-valid answer.</summary>
    public int Answered { get; init; }

    /// <summary>Gets the items answered correctly.</summary>
    public int Correct { get; init; }

    /// <summary>Gets <see cref="Correct"/> over <see cref="Items"/>, 0 to 1.</summary>
    public double Accuracy { get; init; }

    /// <summary>Gets the severe misses.</summary>
    public int SevereMisses { get; init; }

    /// <summary>Gets the answers that failed the schema.</summary>
    public int SchemaInvalid { get; init; }

    /// <summary>Gets the items with no answer: failed or refused.</summary>
    public int Failed { get; init; }

    /// <summary>Gets the answers served from stored inferences.</summary>
    public int FromCache { get; init; }

    /// <summary>Gets the run's cost in micro-USD.</summary>
    public long CostMicroUsd { get; init; }

    /// <summary>Gets the cost per item in micro-USD.</summary>
    public long CostPerItemMicroUsd { get; init; }

    /// <summary>Gets the input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Gets the output tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Gets the cached input tokens.</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Gets the median latency in milliseconds.</summary>
    public long P50LatencyMs { get; init; }

    /// <summary>Gets the 95th-percentile latency in milliseconds.</summary>
    public long P95LatencyMs { get; init; }

    /// <summary>Gets the mean rating of rated items, when any is rated.</summary>
    public double? MeanRating { get; init; }

    /// <summary>Gets accuracy per tag, for slicing by language or kind.</summary>
    public IReadOnlyDictionary<string, double> AccuracyByTag { get; init; } = new Dictionary<string, double>();
}

/// <summary>Represents one item in one run.</summary>
public sealed record EvalItemResult
{
    /// <summary>Gets the run id.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Gets the item id.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Gets the item's subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the item's tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the expected answer.</summary>
    public required string Expected { get; init; }

    /// <summary>Gets the scored fields of the answer, as a JSON object; present only when the report asks for outputs.</summary>
    public string? Answer { get; init; }

    /// <summary>Gets whether every score field matched.</summary>
    public bool Correct { get; init; }

    /// <summary>Gets whether the miss is severe.</summary>
    public bool Severe { get; init; }

    /// <summary>Gets the fields that missed, each <c>{field}:{expected}&gt;{answer}</c>.</summary>
    public IReadOnlyList<string> Misses { get; init; } = [];

    /// <summary>Gets the inference that answered.</summary>
    public Guid? InferenceId { get; init; }

    /// <summary>Gets the item's cost in micro-USD.</summary>
    public long CostMicroUsd { get; init; }

    /// <summary>Gets the latency in milliseconds.</summary>
    public long LatencyMs { get; init; }

    /// <summary>Gets the outcome.</summary>
    public Runs.AiOutcome Outcome { get; init; }

    /// <summary>Gets whether the answer came from a stored inference, with that inference's cost and latency.</summary>
    public bool FromCache { get; init; }

    /// <summary>Gets the rating, when rated.</summary>
    public int? Rating { get; init; }

    /// <summary>Gets the rating's comment.</summary>
    public string? Comment { get; init; }
}
