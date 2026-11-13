using System.Text.Json;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Calls a model once through its provider's chat client and reports text or a classified error, with usage
/// and latency either way.</summary>
internal sealed class AiModelCaller(IOptions<AiProvidersOptions> options, IAiChatClients clients, TimeProvider time) : IAiModelCaller
{
    /// <inheritdoc />
    public bool IsConfigured(string provider) => AiProviderResolution.Describe(options.Value, provider).Problem is null;

    /// <inheritdoc />
    public async Task<AiCallResult> CallAsync(AiCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = time.GetUtcNow();
        var started = time.GetTimestamp();

        var (kind, settings, problem) = AiProviderResolution.Describe(options.Value, request.Model.Provider);
        if (problem is not null)
            return Failed(AiErrorKind.NotConfigured, problem);

        if (!TryBuild(request, kind!, out var messages, out var chatOptions, out var invalid))
            return Failed(AiErrorKind.BadRequest, invalid);

        IChatClient client;
        try
        {
            client = clients.Get(request.Model);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(AiErrorKind.NotConfigured, AiErrorClassifier.Sanitize(exception.Message, settings!.ApiKey));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings!.TimeoutSeconds)));

        ChatResponse response;
        try
        {
            response = await client.GetResponseAsync(messages, chatOptions, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            // An SDK that wraps the caller's cancellation in its own exception still cancels.
            throw new OperationCanceledException("The caller cancelled the model call.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            return Failed(AiErrorClassifier.Classify(exception),
                AiErrorClassifier.Sanitize(AiErrorClassifier.Describe(exception), settings.ApiKey));
        }

        var finishReason = FinishReasonOf(response);
        var answered = AiUsageMapper.Apply(Result() with { FinishReason = finishReason }, response.Usage);

        if (response.FinishReason == ChatFinishReason.ContentFilter || finishReason is "refusal" or "content_filter")
            return answered with
            {
                ErrorKind = AiErrorKind.ContentFiltered,
                Error = $"The model refused or the provider filtered the content (finish reason '{finishReason}').",
            };

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            return answered with
            {
                ErrorKind = AiErrorKind.InvalidResponse,
                Error = $"The provider answered with no text (finish reason '{finishReason ?? "none"}').",
            };

        return answered with { Text = text };

        AiCallResult Result() => new()
        {
            Model = request.Model,
            StartedAtUtc = startedAt,
            LatencyMs = (long)time.GetElapsedTime(started).TotalMilliseconds,
        };

        AiCallResult Failed(AiErrorKind errorKind, string error) => Result() with { ErrorKind = errorKind, Error = error };
    }

    /// <summary>Builds the messages and options of one attempt.</summary>
    /// <param name="request">The attempt.</param>
    /// <param name="kind">The provider's kind.</param>
    /// <param name="messages">The system (when not empty) and user messages.</param>
    /// <param name="chatOptions">The options: model, output budget, temperature, schema, effort.</param>
    /// <param name="problem">Why the request is invalid, when it is.</param>
    /// <returns>Whether the request is valid.</returns>
    internal static bool TryBuild(AiCallRequest request, string kind, out List<ChatMessage> messages, out ChatOptions chatOptions, out string problem)
    {
        messages = [];
        chatOptions = new ChatOptions
        {
            ModelId = request.Model.Model,
            MaxOutputTokens = request.MaxOutputTokens,
            Temperature = (float?)request.Temperature,
        };
        problem = string.Empty;

        if (!string.IsNullOrWhiteSpace(request.System))
        {
            var system = new TextContent(request.System);
            // Anthropic caches up to the end of the marked block; other kinds cache on their own and ignore the mark.
            if (request.CacheSystem && kind == AiProviderKinds.Anthropic)
                system.WithCacheControl(new CacheControlEphemeral());
            messages.Add(new ChatMessage(ChatRole.System, [system]));
        }
        messages.Add(new ChatMessage(ChatRole.User, request.User));

        if (!string.IsNullOrWhiteSpace(request.JsonSchema))
        {
            try
            {
                using var schema = JsonDocument.Parse(request.JsonSchema);
                chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(schema.RootElement.Clone(), request.SchemaName);
            }
            catch (JsonException exception)
            {
                problem = $"The JSON schema does not parse: {exception.Message}";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Effort))
        {
            ReasoningEffort? effort = request.Effort.Trim().ToLowerInvariant() switch
            {
                "none" => ReasoningEffort.None,
                "low" => ReasoningEffort.Low,
                "medium" => ReasoningEffort.Medium,
                "high" => ReasoningEffort.High,
                _ => null,
            };
            if (effort is null)
            {
                problem = $"The effort '{request.Effort}' is not none, low, medium or high.";
                return false;
            }
            chatOptions.Reasoning = new ReasoningOptions { Effort = effort };
        }

        return true;
    }

    /// <summary>Gets the provider's own finish reason: Anthropic's raw <c>stop_reason</c> (<c>end_turn</c>,
    /// <c>refusal</c>), else the normalised one (<c>stop</c>, <c>length</c>, <c>content_filter</c>).</summary>
    /// <param name="response">The response.</param>
    internal static string? FinishReasonOf(ChatResponse response) =>
        response.RawRepresentation is Message { StopReason: { } stopReason } && !string.IsNullOrWhiteSpace(stopReason.Raw())
            ? stopReason.Raw()
            : response.FinishReason?.Value;
}
