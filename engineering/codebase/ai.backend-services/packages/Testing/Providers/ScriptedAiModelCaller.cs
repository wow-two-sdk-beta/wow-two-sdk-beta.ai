using System.Collections.Concurrent;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;

/// <summary>Stands in for the providers in a consumer's tests: answers each call from a queue of scripted results, or from
/// a function, and records every request.</summary>
/// <remarks>
///   - a queued result takes the called model and the clock's start time, whatever the script named, so one script
///     serves any route
///   - with the queue empty, the function answers; with no function, the call fails as <see cref="AiErrorKind.NotConfigured"/>
///   - thread-safe, so an eval run's parallel items can share it
/// </remarks>
/// <param name="time">The clock results are stamped with; the system clock when omitted.</param>
public sealed class ScriptedAiModelCaller(TimeProvider? time = null) : IAiModelCaller
{
    private readonly ConcurrentQueue<AiCallResult> script = new();
    private readonly ConcurrentQueue<AiCallRequest> requests = new();
    private readonly TimeProvider clock = time ?? TimeProvider.System;

    /// <summary>Gets or sets the function that answers when the queue is empty.</summary>
    public Func<AiCallRequest, AiCallResult>? Respond { get; set; }

    /// <summary>Gets or sets the providers <see cref="IsConfigured"/> reports configured; none reports every provider.</summary>
    public ISet<string>? ConfiguredProviders { get; set; }

    /// <summary>Gets every request received, oldest first.</summary>
    public IReadOnlyList<AiCallRequest> Requests => [.. requests];

    /// <summary>Queues results, answered in order.</summary>
    /// <param name="results">The results.</param>
    /// <returns>This caller, for chaining.</returns>
    public ScriptedAiModelCaller Enqueue(params AiCallResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        foreach (var result in results)
            script.Enqueue(result);
        return this;
    }

    /// <inheritdoc />
    public bool IsConfigured(string provider) => ConfiguredProviders is null || ConfiguredProviders.Contains(provider);

    /// <inheritdoc />
    public Task<AiCallResult> CallAsync(AiCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        requests.Enqueue(request);

        var result = script.TryDequeue(out var next) ? next
            : Respond is { } respond ? respond(request)
            : Error(AiErrorKind.NotConfigured, $"The scripted caller has no answer for '{request.Model}'.");
        return Task.FromResult(result with { Model = request.Model, StartedAtUtc = clock.GetUtcNow() });
    }

    /// <summary>Builds an answer.</summary>
    /// <param name="text">The answer text.</param>
    /// <param name="inputTokens">The input tokens, cached ones included.</param>
    /// <param name="outputTokens">The output tokens.</param>
    /// <param name="latencyMs">The latency in milliseconds.</param>
    /// <param name="cacheReadTokens">The input tokens read from the prompt cache.</param>
    /// <param name="cacheWriteTokens">The input tokens written to the prompt cache.</param>
    /// <param name="finishReason">The finish reason.</param>
    public static AiCallResult Answer(string text, long inputTokens = 100, long outputTokens = 20, long latencyMs = 50,
        long cacheReadTokens = 0, long cacheWriteTokens = 0, string finishReason = "end_turn") => new()
    {
        Model = Placeholder,
        StartedAtUtc = DateTimeOffset.UnixEpoch,
        LatencyMs = latencyMs,
        Text = text,
        FinishReason = finishReason,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CacheReadTokens = cacheReadTokens,
        CacheWriteTokens = cacheWriteTokens,
    };

    /// <summary>Builds a failed attempt.</summary>
    /// <param name="kind">Why it failed.</param>
    /// <param name="message">The provider's message.</param>
    /// <param name="latencyMs">The latency in milliseconds.</param>
    public static AiCallResult Error(AiErrorKind kind, string? message = null, long latencyMs = 10) => new()
    {
        Model = Placeholder,
        StartedAtUtc = DateTimeOffset.UnixEpoch,
        LatencyMs = latencyMs,
        ErrorKind = kind,
        Error = message ?? kind.ToString(),
    };

    /// <summary>Builds a refusal: the provider filtered the content.</summary>
    /// <param name="message">The provider's message.</param>
    public static AiCallResult Refusal(string? message = null) => Error(AiErrorKind.ContentFiltered, message ?? "The model refused.");

    private static AiModelRef Placeholder { get; } = new("scripted", "scripted");
}
