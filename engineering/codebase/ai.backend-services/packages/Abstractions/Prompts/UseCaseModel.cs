namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Represents one AI use case: how it keeps prompts, what its answer looks like, and which models it tries.</summary>
public sealed record UseCaseModel
{
    /// <summary>Gets the key: lowercase letters, digits and hyphens, at most <see cref="AiConstants.MaxKeyLength"/>.</summary>
    public required string Key { get; init; }

    /// <summary>Gets what the use case does.</summary>
    public string? Description { get; init; }

    /// <summary>Gets how the use case keeps its prompts.</summary>
    public PromptMode PromptMode { get; init; } = PromptMode.Rewrite;

    /// <summary>Gets the answer's JSON schema as text; an object schema every answer must match. None takes free text.</summary>
    public string? OutputSchema { get; init; }

    /// <summary>Gets the models to try in order, each <c>{provider}/{model}</c>; a later one runs when an earlier one fails.</summary>
    public IReadOnlyList<string> Route { get; init; } = [];

    /// <summary>Gets whether model answers are kept in the inference log.</summary>
    public bool KeepOutputs { get; init; } = true;

    /// <summary>Gets when the use case last changed (UTC).</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
