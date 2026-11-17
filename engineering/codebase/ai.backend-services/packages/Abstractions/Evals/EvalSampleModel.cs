namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents which of a set's items a run takes: an explicit subset, the items carrying every tag, and a
/// deterministic sample of them.</summary>
/// <remarks>The filters apply first (<see cref="ItemIds"/>, then <see cref="Tags"/>), then <see cref="Count"/> keeps that
/// many, ranked by a hash of <see cref="Seed"/> and each item's id: the same seed keeps the same items, and items added
/// later never push out ones a rerun took, unless they rank ahead of them.</remarks>
public sealed record EvalSampleModel
{
    /// <summary>Gets how many items to keep; none keeps every item that passes the filters.</summary>
    public int? Count { get; init; }

    /// <summary>Gets the seed the sample is ranked by; none takes <see cref="AiConstants.DefaultSampleSeed"/>, so reruns
    /// compare the same items.</summary>
    public int? Seed { get; init; }

    /// <summary>Gets the tags an item must carry, all of them.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the items to take; none takes the whole set. Every id must be an item of the set.</summary>
    public IReadOnlyList<Guid> ItemIds { get; init; } = [];
}
