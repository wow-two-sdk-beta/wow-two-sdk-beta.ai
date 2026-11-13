namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents one cell of an eval matrix: a set run through one prompt version on one model.</summary>
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
}
