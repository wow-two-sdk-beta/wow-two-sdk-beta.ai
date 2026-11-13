namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Represents a prompt to save: in place under <see cref="PromptMode.Rewrite"/>, as the next version under
/// <see cref="PromptMode.Versioned"/>.</summary>
public sealed record PromptDraft
{
    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the models the row serves; none for every model.</summary>
    public string? ModelMatch { get; init; }

    /// <summary>Gets the system message.</summary>
    public required string SystemTemplate { get; init; }

    /// <summary>Gets the user message.</summary>
    public required string UserTemplate { get; init; }

    /// <summary>Gets the call parameters.</summary>
    public PromptParameters Parameters { get; init; } = new();

    /// <summary>Gets why it changed.</summary>
    public string? Note { get; init; }

    /// <summary>Gets who saved it.</summary>
    public string? By { get; init; }
}
