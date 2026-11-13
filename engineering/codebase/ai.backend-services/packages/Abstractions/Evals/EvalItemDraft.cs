namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an item to add: give <see cref="Input"/>, or a <see cref="Subject"/> a registered
/// <see cref="IEvalSubjectSource"/> turns into the input once.</summary>
public sealed record EvalItemDraft
{
    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets what the item is taken from.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the prompt variables; none resolves them from <see cref="Subject"/>.</summary>
    public IReadOnlyDictionary<string, string?>? Input { get; init; }

    /// <summary>Gets the expected answer as a JSON object.</summary>
    public required string Expected { get; init; }

    /// <summary>Gets free tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets who set the expected answer.</summary>
    public string? LabelledBy { get; init; }
}
