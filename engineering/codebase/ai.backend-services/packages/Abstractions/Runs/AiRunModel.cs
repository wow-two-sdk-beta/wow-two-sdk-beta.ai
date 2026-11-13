using WoW.Two.Sdk.Beta.Be.Ai.Inferences;

namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Represents a run's result: the answer when one passed the schema, and every attempt it took.</summary>
public sealed record AiRunModel
{
    /// <summary>Gets how the run ended: the last attempt's outcome.</summary>
    public required AiOutcome Outcome { get; init; }

    /// <summary>Gets the answer as JSON text, when <see cref="Outcome"/> is <see cref="AiOutcome.Succeeded"/>.</summary>
    public string? Output { get; init; }

    /// <summary>Gets every attempt in order, fallbacks included; the last one decided the outcome.</summary>
    public required IReadOnlyList<InferenceModel> Attempts { get; init; }

    /// <summary>Gets whether the answer came from a stored inference with the same prompt, model and input.</summary>
    public bool FromCache { get; init; }

    /// <summary>Gets the attempt that decided the outcome.</summary>
    public InferenceModel Final => Attempts[^1];

    /// <summary>Gets the run's total cost in micro-USD, over every attempt.</summary>
    public long CostMicroUsd => Attempts.Sum(attempt => attempt.CostMicroUsd);
}
