namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an eval set with its items, to carry from a product's database to the eval bench.</summary>
/// <remarks>It holds item inputs, which may be harvested text: it moves between databases, never into a repo.</remarks>
public sealed record EvalSetExport
{
    /// <summary>Gets the format, <c>wow2.ai.eval-set.v1</c>.</summary>
    public string Format { get; init; } = FormatV1;

    /// <summary>Gets the set.</summary>
    public required EvalSetModel Set { get; init; }

    /// <summary>Gets the items.</summary>
    public required IReadOnlyList<EvalItemModel> Items { get; init; }

    /// <summary>The format name of this version.</summary>
    public const string FormatV1 = "wow2.ai.eval-set.v1";
}
