namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents a filter over eval runs, newest first.</summary>
public sealed record EvalRunQuery
{
    /// <summary>Gets the set to keep.</summary>
    public string? Set { get; init; }

    /// <summary>Gets the batch to keep.</summary>
    public Guid? BatchId { get; init; }

    /// <summary>Gets the status to keep.</summary>
    public EvalRunStatus? Status { get; init; }

    /// <summary>Gets the most rows to return, 1 to 1000.</summary>
    public int Limit { get; init; } = 100;

    /// <summary>Gets the data scopes to keep, <see cref="AiConstants.Unscoped"/> for runs of unscoped sets; none keeps
    /// every scope, an empty list keeps nothing.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }
}
