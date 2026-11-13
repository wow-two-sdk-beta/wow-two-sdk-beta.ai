namespace WoW.Two.Sdk.Beta.Be.Ai.Inferences;

/// <summary>Represents the inference log summed per use case, provider and model over a period.</summary>
public sealed record InferenceTotals
{
    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the provider name.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model id.</summary>
    public required string Model { get; init; }

    /// <summary>Gets the attempt count.</summary>
    public long Attempts { get; init; }

    /// <summary>Gets the attempts that succeeded.</summary>
    public long Succeeded { get; init; }

    /// <summary>Gets the input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Gets the output tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Gets the cost in micro-USD.</summary>
    public long CostMicroUsd { get; init; }
}
