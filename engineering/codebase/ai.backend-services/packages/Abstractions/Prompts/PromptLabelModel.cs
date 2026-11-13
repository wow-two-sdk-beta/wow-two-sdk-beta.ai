namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Represents a label's position: which version of a versioned use case it points at.</summary>
public sealed record PromptLabelModel
{
    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the label, such as <see cref="AiConstants.ProductionLabel"/>.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the version the label points at.</summary>
    public required int Version { get; init; }

    /// <summary>Gets when the label moved there (UTC).</summary>
    public DateTimeOffset MovedAtUtc { get; init; }

    /// <summary>Gets who moved it.</summary>
    public string? MovedBy { get; init; }
}
