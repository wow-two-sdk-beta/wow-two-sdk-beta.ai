namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents a rating of one item's answer in one run, by a person or an agent.</summary>
public sealed record EvalRatingDraft
{
    /// <summary>Gets the run id.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Gets the item id.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Gets the rating, 1 (wrong) to 5 (exactly right).</summary>
    public required int Rating { get; init; }

    /// <summary>Gets why.</summary>
    public string? Comment { get; init; }

    /// <summary>Gets who rated it.</summary>
    public string? By { get; init; }
}
