using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Wire;

/// <summary>Writes and reads the API's wire: success bodies under <c>data</c> on the be sdk's JSON preset (camelCase,
/// enums as camelCase words, dictionary keys as written, nulls left out), failures as the be sdk's problem details
/// (<c>code</c> holds the error type), and request bodies patched with their route values.</summary>
internal static class AiHttp
{
    /// <summary>Gets the JSON every body is written and read with.</summary>
    public static JsonSerializerOptions Json => JsonOptionsConstants.Default;

    /// <summary>Writes a value under <c>data</c>.</summary>
    public static IResult Ok<T>(T value, int status = StatusCodes.Status200OK) =>
        Results.Json(new AiEnvelope<T>(value), Json, statusCode: status);

    /// <summary>Writes a broker's result: its value under <c>data</c>, or its error as problem details.</summary>
    public static IResult Reply<T>(HttpContext context, Result<T> result) where T : notnull => result switch
    {
        Result<T>.Success { Value: var value } => Ok(value),
        Result<T>.Failure { Error: var error } => Problem(context, error),
        _ => throw new InvalidOperationException("A result is either a success or a failure."),
    };

    /// <summary>Writes an error as the be sdk's problem details, through the host's registered factory when it has one.</summary>
    public static IResult Problem(HttpContext context, AppError error)
    {
        var factory = context.RequestServices.GetService<IAppErrorProblemDetailsFactory>()
            ?? new AppErrorProblemDetailsFactory(new ErrorHttpStatusCodeMapper(), new ErrorMessageMapper(), new FieldErrorMessageMapper());
        return Results.Problem(factory.Create(error, context));
    }

    /// <summary>Answers a read of something outside the caller's data scopes as missing, so it reveals nothing.</summary>
    public static IResult Hidden(HttpContext context, AppError notFound) => Problem(context, notFound);

    /// <summary>Answers a write or a read naming a data scope the caller may not use.</summary>
    public static IResult ForbiddenScope(HttpContext context) =>
        Problem(context, AppErrorFactory.Forbidden("The caller may not use that data scope."));

    /// <summary>Reads the request body as <typeparamref name="T"/>, after <paramref name="patch"/> set the route's values
    /// on it.</summary>
    /// <returns>The value, or the problem to answer: a body that is not a JSON object of that shape.</returns>
    public static async Task<(T? Value, IResult? Problem)> ReadAsync<T>(HttpContext context, Action<JsonObject>? patch = null) where T : class
    {
        try
        {
            var node = await JsonSerializer.DeserializeAsync<JsonNode>(context.Request.Body, Json, context.RequestAborted);
            if (node is not JsonObject body)
                return (null, Problem(context, AiErrors.Invalid($"The body must be a JSON object ({typeof(T).Name}).")));

            patch?.Invoke(body);
            return body.Deserialize<T>(Json) is { } value
                ? (value, null)
                : (null, Problem(context, AiErrors.Invalid($"The body is not a {typeof(T).Name}.")));
        }
        catch (JsonException exception)
        {
            return (null, Problem(context, AiErrors.Invalid($"The body is not a {typeof(T).Name}: {exception.Message}")));
        }
    }

    /// <summary>Gets the caller's data-scope grant from the host's <see cref="IAiScopeAccess"/>.</summary>
    public static ValueTask<AiScopeGrant> GrantAsync(HttpContext context) =>
        context.RequestServices.GetRequiredService<IAiScopeAccess>().GrantAsync(context, context.RequestAborted);

    /// <summary>Reads the data scopes a list names (<see cref="AiRouteConstants.ScopeParameter"/>, repeatable; an empty
    /// value names the unscoped bucket); none when it names none.</summary>
    public static IReadOnlyList<string>? Scopes(HttpContext context) =>
        context.Request.Query.TryGetValue(AiRouteConstants.ScopeParameter, out var values)
            ? [.. values.Select(value => value ?? AiConstants.Unscoped)]
            : null;

    /// <summary>Reads a query value.</summary>
    public static string? Query(HttpContext context, string name) =>
        context.Request.Query.TryGetValue(name, out var values) && values.Count > 0 && !string.IsNullOrWhiteSpace(values[^1]) ? values[^1] : null;

    /// <summary>Reads every value of a repeatable query parameter, each split on commas.</summary>
    public static IReadOnlyList<string> QueryList(HttpContext context, string name) =>
        context.Request.Query.TryGetValue(name, out var values)
            ? [.. values.SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))]
            : [];

    /// <summary>Sets a property of a body, replacing what the caller sent.</summary>
    public static void Set(JsonObject body, string name, JsonNode? value)
    {
        foreach (var key in body.Select(pair => pair.Key).Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)).ToList())
            body.Remove(key);
        body[name] = value;
    }
}
