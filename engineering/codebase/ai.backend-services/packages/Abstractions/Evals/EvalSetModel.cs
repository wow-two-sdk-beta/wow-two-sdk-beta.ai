namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an eval set: frozen inputs of one use case with their expected answers, and how to score them.</summary>
public sealed record EvalSetModel
{
    /// <summary>Gets the key: lowercase letters, digits and hyphens, such as <c>afisha-10</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the use case the set evaluates.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the answer fields compared with each item's expected answer; an item is correct when all match
    /// (string values compared case-insensitively). None compares every field the expected answer names.</summary>
    public IReadOnlyList<string> ScoreFields { get; init; } = [];

    /// <summary>Gets the misses counted as severe, each <c>{field}:{expected}&gt;{answer}</c>, such as
    /// <c>relevance:close&gt;none</c>.</summary>
    public IReadOnlyList<string> SevereMisses { get; init; } = [];

    /// <summary>Gets what the set holds and why.</summary>
    public string? Note { get; init; }

    /// <summary>Gets when the set was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }
}
