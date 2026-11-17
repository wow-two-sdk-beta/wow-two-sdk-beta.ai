namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Represents one call of a use case: its prompt variables, and what to record it against.</summary>
public sealed record AiRunRequest
{
    /// <summary>Gets the use case key, such as <c>ocharo-relevance</c>.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the values for the prompt's <c>{{name}}</c> placeholders.</summary>
    public IReadOnlyDictionary<string, string?> Variables { get; init; } = new Dictionary<string, string?>();

    /// <summary>Gets the images to send with the user message, in order: references the module resolves to bytes. A prompt
    /// takes the first <see cref="Prompts.PromptParameters.MaxImages"/> of them and ignores the rest; one that takes none
    /// ignores them all. More than the module's limit is refused before any call.</summary>
    public IReadOnlyList<AiImageRef> Images { get; init; } = [];

    /// <summary>Gets what the call is about, such as <c>harvest:post:{id}@{revision}</c>; recorded on every attempt.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets a prompt version to use instead of the label's; versioned use cases only.</summary>
    public int? Version { get; init; }

    /// <summary>Gets a label to resolve instead of <see cref="AiConstants.ProductionLabel"/>; versioned use cases only.</summary>
    public string? Label { get; init; }

    /// <summary>Gets model references (<c>{provider}/{model}</c>) to try instead of the use case's route, in order.</summary>
    public IReadOnlyList<string>? Models { get; init; }

    /// <summary>Gets whether to call the model even when an answer for the same prompt, model and input is stored.</summary>
    public bool Fresh { get; init; }

    /// <summary>Gets the eval run the call belongs to, when an eval runs it.</summary>
    public Guid? EvalRunId { get; init; }

    /// <summary>Gets the data scope the call belongs to, such as <c>tenant:{id}</c>; none for the unscoped bucket. Recorded
    /// on every attempt; a stored answer is reused only within the same scope. Use cases and prompts stay shared.</summary>
    public string? Scope { get; init; }
}
