using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Represents one model attempt's result: the text, or a classified error, with usage and latency either way.</summary>
public sealed record AiCallResult
{
    /// <summary>Gets the model called.</summary>
    public required AiModelRef Model { get; init; }

    /// <summary>Gets when the attempt started (UTC).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets the attempt's wall-clock time in milliseconds.</summary>
    public required long LatencyMs { get; init; }

    /// <summary>Gets the answer text; none when the attempt failed.</summary>
    public string? Text { get; init; }

    /// <summary>Gets why the attempt failed; none when it answered.</summary>
    public AiErrorKind? ErrorKind { get; init; }

    /// <summary>Gets the provider's message, trimmed to 500 characters, when the attempt failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the provider's finish reason, such as <c>end_turn</c> or <c>refusal</c>.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Gets the input tokens, cached ones included.</summary>
    public long InputTokens { get; init; }

    /// <summary>Gets the output tokens, reasoning included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Gets the input tokens read from the prompt cache.</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Gets the input tokens written to the prompt cache.</summary>
    public long CacheWriteTokens { get; init; }

    /// <summary>Gets the reasoning tokens inside <see cref="OutputTokens"/>.</summary>
    public long ReasoningTokens { get; init; }

    /// <summary>Gets whether the attempt answered with text.</summary>
    public bool Succeeded => ErrorKind is null && Text is not null;
}
