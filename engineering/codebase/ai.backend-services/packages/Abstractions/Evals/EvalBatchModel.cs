namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents a queued matrix: the items and scoring rules every cell shares, pinned when it was queued, and its
/// runs.</summary>
/// <remarks>A worker runs exactly the pinned items, scored against the expected answers and rules as they were when the
/// batch was queued, with each cell's pinned prompt; a later relabel, rewrite or label move changes later batches only.</remarks>
public sealed record EvalBatchModel
{
    /// <summary>Gets the batch id, shared by every run of the matrix.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the data scope the batch belongs to: its set's; none for the unscoped bucket.</summary>
    public string? Scope { get; init; }

    /// <summary>Gets the items every run takes, in the order they run.</summary>
    public required IReadOnlyList<Guid> ItemIds { get; init; }

    /// <summary>Gets the score fields pinned from the set.</summary>
    public IReadOnlyList<string> ScoreFields { get; init; } = [];

    /// <summary>Gets the severe misses pinned from the set.</summary>
    public IReadOnlyList<string> SevereMisses { get; init; } = [];

    /// <summary>Gets how the items were asked for; none when the batch takes the whole set.</summary>
    public EvalSampleModel? Sample { get; init; }

    /// <summary>Gets who asked for the batch.</summary>
    public string? RequestedBy { get; init; }

    /// <summary>Gets when the batch was queued (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets the batch's runs, one per cell, in matrix order.</summary>
    public required IReadOnlyList<EvalRunModel> Runs { get; init; }
}
