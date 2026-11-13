using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Inferences;

/// <summary>Represents one model attempt as the inference log keeps it.</summary>
public sealed record InferenceModel
{
    /// <summary>Gets the inference id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets when the attempt started (UTC).</summary>
    public required DateTimeOffset AtUtc { get; init; }

    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the prompt version used.</summary>
    public required int PromptVersion { get; init; }

    /// <summary>Gets the prompt row's content hash; tells two texts apart when a rewrite kept the version.</summary>
    public required string PromptHash { get; init; }

    /// <summary>Gets the prompt's model match, when a model-specific row answered.</summary>
    public string? PromptModelMatch { get; init; }

    /// <summary>Gets the provider name, such as <c>anthropic</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model id, such as <c>claude-haiku-5-5</c>.</summary>
    public required string Model { get; init; }

    /// <summary>Gets the attempt's position in its run, from 1.</summary>
    public required int Attempt { get; init; }

    /// <summary>Gets what the call was about.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the SHA-256 of the rendered messages, lowercase hex.</summary>
    public required string InputHash { get; init; }

    /// <summary>Gets the input tokens, cached ones included.</summary>
    public long InputTokens { get; init; }

    /// <summary>Gets the output tokens, reasoning included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Gets the input tokens read from the provider's prompt cache.</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Gets the input tokens written to the provider's prompt cache.</summary>
    public long CacheWriteTokens { get; init; }

    /// <summary>Gets the reasoning tokens inside <see cref="OutputTokens"/>.</summary>
    public long ReasoningTokens { get; init; }

    /// <summary>Gets the cost in micro-USD, from the price in effect at <see cref="AtUtc"/>; 0 when no price is known.</summary>
    public long CostMicroUsd { get; init; }

    /// <summary>Gets whether a price was known for the model.</summary>
    public bool Priced { get; init; }

    /// <summary>Gets the attempt's wall-clock time in milliseconds.</summary>
    public long LatencyMs { get; init; }

    /// <summary>Gets how the attempt ended.</summary>
    public required AiOutcome Outcome { get; init; }

    /// <summary>Gets why the attempt failed, when it did.</summary>
    public AiErrorKind? ErrorKind { get; init; }

    /// <summary>Gets the provider's or parser's message, trimmed, when the attempt failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the provider's finish reason, such as <c>end_turn</c>.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Gets the answer text, when the module keeps outputs.</summary>
    public string? Output { get; init; }

    /// <summary>Gets the eval run the attempt belongs to.</summary>
    public Guid? EvalRunId { get; init; }
}
