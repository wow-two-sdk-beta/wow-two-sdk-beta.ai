namespace WoW.Two.Sdk.Beta.Be.Ai.Inferences;

/// <summary>Represents the period and data scopes the inference log is summed over.</summary>
public sealed record InferenceTotalsQuery
{
    /// <summary>Gets the start (UTC, inclusive).</summary>
    public required DateTimeOffset FromUtc { get; init; }

    /// <summary>Gets the end (UTC, exclusive); it must come after <see cref="FromUtc"/>.</summary>
    public required DateTimeOffset ToUtc { get; init; }

    /// <summary>Gets the data scopes to keep, <see cref="AiConstants.Unscoped"/> for calls made without one; none keeps
    /// every scope, an empty list keeps nothing.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }
}
