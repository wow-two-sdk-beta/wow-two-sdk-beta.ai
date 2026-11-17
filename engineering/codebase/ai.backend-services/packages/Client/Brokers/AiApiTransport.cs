using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Calls the Ai HTTP API: writes bodies and reads <c>data</c> envelopes on the be sdk's JSON preset, and turns
/// problem details back into the contract's errors.</summary>
/// <remarks>
///   - a problem's <c>code</c> names the error type; without one the status decides (400 validation, 401, 403, 404, 409,
///     429), anything else reads as the service being unavailable
///   - an unreachable service is <see cref="AppErrorType.ExternalUnavailable"/>, malformed JSON
///     <see cref="AppErrorType.SerializationFailed"/>; a cancellation by the caller throws
/// </remarks>
/// <param name="http">The named client <see cref="AiClientConstants.HttpClientName"/>, rooted at the service.</param>
internal sealed class AiApiTransport(HttpClient http)
{
    private static JsonSerializerOptions Json => JsonOptionsConstants.Default;

    /// <summary>Sends a request and reads its <c>data</c>.</summary>
    public async Task<Result<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken) where T : notnull
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result<T>.Fail(await ReadProblemAsync(response, cancellationToken));

            var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, cancellationToken);
            return envelope is { Data: { } data }
                ? Result<T>.Ok(data)
                : Result<T>.Fail(AppErrorFactory.SerializationFailed("The Ai service answered without data."));
        }
        catch (HttpRequestException exception)
        {
            return Result<T>.Fail(AppErrorFactory.ExternalUnavailable($"The Ai service is unreachable: {exception.Message}"));
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Fail(AppErrorFactory.OperationTimeout($"The Ai service did not answer in time: {exception.Message}"));
        }
        catch (JsonException exception)
        {
            return Result<T>.Fail(AppErrorFactory.SerializationFailed("The Ai service answered malformed JSON.", exception));
        }
    }

    /// <summary>Reads a resource.</summary>
    public Task<Result<T>> GetAsync<T>(string path, CancellationToken cancellationToken) where T : notnull =>
        SendAsync<T>(HttpMethod.Get, path, body: null, cancellationToken);

    /// <summary>Builds a path with a query: each pair once per value, escaped; a null value is left out.</summary>
    public static string Path(string route, params (string Name, string? Value)[] query)
    {
        var text = new StringBuilder(route);
        var first = true;
        foreach (var (name, value) in query)
        {
            if (value is null)
                continue;
            text.Append(first ? '?' : '&').Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
            first = false;
        }

        return text.ToString();
    }

    /// <summary>Writes the data scopes of a filter as repeated <see cref="AiRouteConstants.ScopeParameter"/> values.</summary>
    public static IEnumerable<(string Name, string? Value)> Scopes(IReadOnlyList<string>? scopes) =>
        (scopes ?? []).Select(scope => (AiRouteConstants.ScopeParameter, (string?)(scope ?? AiConstants.Unscoped)));

    /// <summary>Writes an instant for a query: UTC, round-trip.</summary>
    public static string? Instant(DateTimeOffset? instant) => instant?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Refuses a blank path value before a call: its route would not match.</summary>
    public static AppError? Blank(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? AiErrors.Invalid($"A {what} is required.") : null;

    private static async Task<AppError> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<Problem>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            // A body that is no problem: the status alone decides.
        }

        var type = problem?.Code is { } code && Enum.TryParse<AppErrorType>(code, ignoreCase: true, out var parsed) ? parsed : ByStatus(response.StatusCode);
        var message = problem?.Detail ?? problem?.Title ?? $"The Ai service answered {(int)response.StatusCode}.";
        return AppError.Of(type, message);
    }

    private static AppErrorType ByStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => AppErrorType.Validation,
        HttpStatusCode.Unauthorized => AppErrorType.Unauthorized,
        HttpStatusCode.Forbidden => AppErrorType.Forbidden,
        HttpStatusCode.NotFound => AppErrorType.NotFound,
        HttpStatusCode.Conflict => AppErrorType.Conflict,
        HttpStatusCode.TooManyRequests => AppErrorType.TooManyRequests,
        _ => AppErrorType.ExternalUnavailable,
    };

    /// <summary>Represents the service's success envelope.</summary>
    private sealed record Envelope<T>(T? Data);

    /// <summary>Represents the parts of a problem response the client reads.</summary>
    private sealed record Problem(string? Code, string? Detail, string? Title);
}
