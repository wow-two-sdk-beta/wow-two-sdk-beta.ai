namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents a finished eval run: which prompt and model answered a set.</summary>
public sealed record EvalRunModel
{
    /// <summary>Gets the run id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the prompt version the run used.</summary>
    public required int PromptVersion { get; init; }

    /// <summary>Gets the prompt's content hash.</summary>
    public required string PromptHash { get; init; }

    /// <summary>Gets the provider name.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model id.</summary>
    public required string Model { get; init; }

    /// <summary>Gets when the run started (UTC).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets when the run finished (UTC).</summary>
    public DateTimeOffset? FinishedAtUtc { get; init; }

    /// <summary>Gets what the run tries.</summary>
    public string? Note { get; init; }
}
