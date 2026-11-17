namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents one cell of an eval matrix: a set run through one prompt version on one model.</summary>
/// <remarks>The sample fields pick the items (<see cref="EvalSampleModel"/>): <see cref="ItemIds"/>, then
/// <see cref="Tags"/>, then <see cref="Count"/> ranked by <see cref="Seed"/>. A sampled run scores and reports only the
/// items it took.</remarks>
public sealed record EvalRunRequest
{
    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the model, <c>{provider}/{model}</c>; the run never falls back to another.</summary>
    public required string Model { get; init; }

    /// <summary>Gets the prompt version; versioned use cases only.</summary>
    public int? Version { get; init; }

    /// <summary>Gets the label to resolve instead; versioned use cases only.</summary>
    public string? Label { get; init; }

    /// <summary>Gets whether to call the model even for inputs answered before with the same prompt and model.</summary>
    public bool Fresh { get; init; }

    /// <summary>Gets how many items run at once, 1 (the default) up to the module's limit.</summary>
    public int Parallelism { get; init; } = 1;

    /// <summary>Gets what the run tries.</summary>
    public string? Note { get; init; }

    /// <summary>Gets how many items to sample, 1 or more; none takes every item that passes the filters.</summary>
    public int? Count { get; init; }

    /// <summary>Gets the seed the sample is ranked by; none takes <see cref="AiConstants.DefaultSampleSeed"/>, so reruns
    /// compare the same items.</summary>
    public int? Seed { get; init; }

    /// <summary>Gets the tags an item must carry, all of them; none keeps every item.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets an explicit subset of the set's items; none takes the whole set.</summary>
    public IReadOnlyList<Guid> ItemIds { get; init; } = [];

    /// <summary>Gets who asked for the run: a person, a client or an agent.</summary>
    public string? RequestedBy { get; init; }
}
