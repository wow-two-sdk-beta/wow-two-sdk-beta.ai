using System.ClientModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Anthropic.Exceptions;
using OllamaSharp.Models.Exceptions;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Maps a provider failure onto an <see cref="AiErrorKind"/> and a short, key-free message.</summary>
internal static class AiErrorClassifier
{
    /// <summary>The longest error message kept.</summary>
    internal const int MaxErrorLength = 500;

    /// <summary>Classifies an HTTP status: 401/403 unauthorized, 408 timeout, 429 rate limited, 5xx (Anthropic's 529
    /// included) unavailable, any other 4xx a bad request.</summary>
    /// <param name="status">The HTTP status code.</param>
    internal static AiErrorKind ClassifyStatus(int status) => status switch
    {
        401 or 403 => AiErrorKind.Unauthorized,
        408 => AiErrorKind.Timeout,
        429 => AiErrorKind.RateLimited,
        >= 500 and <= 599 => AiErrorKind.Unavailable,
        >= 400 and <= 499 => AiErrorKind.BadRequest,
        _ => AiErrorKind.Unknown,
    };

    /// <summary>Classifies an exception a provider SDK threw; the caller's own cancellation never reaches here.</summary>
    /// <param name="exception">The failure.</param>
    internal static AiErrorKind Classify(Exception exception) => exception switch
    {
        AnthropicApiException api => ClassifyStatus((int)api.StatusCode),
        AnthropicIOException => AiErrorKind.Network,
        AnthropicInvalidDataException => AiErrorKind.InvalidResponse,
        ClientResultException { Status: > 0 } result => ClassifyStatus(result.Status),
        ClientResultException { InnerException: { } inner } => Classify(inner),
        HttpRequestException { StatusCode: { } status } => ClassifyStatus((int)status),
        HttpRequestException => AiErrorKind.Network,
        ResponseError => AiErrorKind.Unknown,
        OllamaException => AiErrorKind.BadRequest,
        TimeoutException => AiErrorKind.Timeout,
        OperationCanceledException => AiErrorKind.Timeout,
        SocketException or IOException => AiErrorKind.Network,
        JsonException => AiErrorKind.InvalidResponse,
        _ when exception.InnerException is { } inner => Classify(inner),
        _ => AiErrorKind.Unknown,
    };

    /// <summary>Gets an exception's message: the provider's response body where the SDK keeps one.</summary>
    /// <param name="exception">The failure.</param>
    internal static string Describe(Exception exception) => exception switch
    {
        AnthropicApiException api when api.ResponseBody.Length > 0 => $"{(int)api.StatusCode}: {api.ResponseBody}",
        OperationCanceledException => "The call exceeded the provider's timeout.",
        _ => exception.Message,
    };

    /// <summary>Removes the key, folds whitespace and trims the message to <see cref="MaxErrorLength"/> characters.</summary>
    /// <param name="message">The raw message.</param>
    /// <param name="apiKey">The provider's key, never echoed.</param>
    internal static string Sanitize(string? message, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "The provider gave no message.";
        if (!string.IsNullOrEmpty(apiKey))
            message = message.Replace(apiKey, "***", StringComparison.Ordinal);

        var folded = new StringBuilder(Math.Min(message.Length, MaxErrorLength + 1));
        var space = false;
        foreach (var c in message.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space)
                folded.Append(' ');
            space = false;
            folded.Append(c);
            if (folded.Length > MaxErrorLength)
                break;
        }
        return folded.Length > MaxErrorLength ? folded.ToString(0, MaxErrorLength - 1) + "…" : folded.ToString();
    }
}
