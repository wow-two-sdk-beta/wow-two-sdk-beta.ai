namespace WoW.Two.Sdk.Beta.Be.Ai.Inferences;

/// <summary>Represents a filter over the inference log, newest first.</summary>
public sealed record InferenceQuery
{
    /// <summary>Gets the use case to keep.</summary>
    public string? UseCase { get; init; }

    /// <summary>Gets the subject to keep.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the eval run to keep.</summary>
    public Guid? EvalRunId { get; init; }

    /// <summary>Gets the earliest start time to keep (UTC, inclusive).</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the latest start time to keep (UTC, exclusive).</summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets the most rows to return, 1 to 1000.</summary>
    public int Limit { get; init; } = 100;
}
