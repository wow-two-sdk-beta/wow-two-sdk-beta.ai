namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents one eval item: the prompt variables frozen when it was added, and the expected answer.</summary>
/// <remarks>The input stays in the database it was added to; an export carries it to the eval bench, never to a repo.</remarks>
public sealed record EvalItemModel
{
    /// <summary>Gets the item id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets what the item was taken from, such as <c>harvest:post:{id}@{revision}</c>.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the prompt variables.</summary>
    public required IReadOnlyDictionary<string, string?> Input { get; init; }

    /// <summary>Gets the expected answer as a JSON object, such as <c>{"relevance":"close","kind":"runway"}</c>.</summary>
    public required string Expected { get; init; }

    /// <summary>Gets free tags for slicing the report, such as <c>lang:ru</c> or <c>slice:positive</c>.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets who set the expected answer.</summary>
    public string? LabelledBy { get; init; }
}
