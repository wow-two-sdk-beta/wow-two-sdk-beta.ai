namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an eval matrix to queue: every model × every prompt version or label, one run per cell, all
/// sharing a batch id and one sample.</summary>
/// <remarks>With neither <see cref="Versions"/> nor <see cref="Labels"/>, each model runs the version a call without them
/// resolves (the production label's, else the newest); with both, the cells are models × (versions, then labels).</remarks>
public sealed record EvalMatrixRequest
{
    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the models, each <c>{provider}/{model}</c>; at least one.</summary>
    public required IReadOnlyList<string> Models { get; init; }

    /// <summary>Gets the prompt versions; versioned use cases only.</summary>
    public IReadOnlyList<int> Versions { get; init; } = [];

    /// <summary>Gets the labels to resolve; versioned use cases only. Each resolves when the run is queued.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Gets whether to call the model even for inputs answered before with the same prompt and model.</summary>
    public bool Fresh { get; init; }

    /// <summary>Gets how many items each run sends at once, 1 (the default) up to the module's limit.</summary>
    public int Parallelism { get; init; } = 1;

    /// <summary>Gets what the batch tries.</summary>
    public string? Note { get; init; }

    /// <summary>Gets how many items to sample, 1 or more; none takes every item that passes the filters.</summary>
    public int? Count { get; init; }

    /// <summary>Gets the seed the sample is ranked by; none takes <see cref="AiConstants.DefaultSampleSeed"/>.</summary>
    public int? Seed { get; init; }

    /// <summary>Gets the tags an item must carry, all of them.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets an explicit subset of the set's items.</summary>
    public IReadOnlyList<Guid> ItemIds { get; init; } = [];

    /// <summary>Gets who asked for the batch.</summary>
    public string? RequestedBy { get; init; }
}
