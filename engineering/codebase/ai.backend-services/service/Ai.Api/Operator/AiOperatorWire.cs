using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Ai.Api.Operator;

/// <summary>The <c>ai</c> verbs' output conventions: one JSON line per verb, <c>{"ok":true,"command":…,"data":…}</c> on
/// success and <c>{"ok":false,"command":…,"error":…,"message":…}</c> otherwise.</summary>
/// <remarks>Exit codes follow the be sdk's verbs: 0 done, 1 refused (usage, not found, invalid, a run that got no
/// answer), 2 failed (the database or a file could not be reached).</remarks>
public static class AiOperatorWire
{
    /// <summary>The exit code of a verb that did its work.</summary>
    public const int Done = 0;

    /// <summary>The exit code of a verb that refused: bad arguments, a missing record, or no answer.</summary>
    public const int Refused = 1;

    /// <summary>The exit code of a verb that failed: the database, a file or another dependency could not be reached.</summary>
    public const int Failed = 2;

    /// <summary>Gets the JSON the verbs print: camelCase names, enums as camelCase words, nulls left out, one line.</summary>
    public static JsonSerializerOptions Options { get; } = Create(indented: false);

    /// <summary>Gets the JSON an export file is written with: as <see cref="Options"/>, indented.</summary>
    public static JsonSerializerOptions FileOptions { get; } = Create(indented: true);

    /// <summary>Reads the database the verbs run against: <c>DB_CONNECTION</c>, else <c>DatabaseSettings:ConnectionString</c>.</summary>
    /// <param name="configuration">The settings.</param>
    public static string? ConnectionString(IConfiguration configuration) =>
        configuration["DB_CONNECTION"] is { Length: > 0 } alias ? alias : configuration["DatabaseSettings:ConnectionString"];

    /// <summary>Writes a success line.</summary>
    /// <param name="output">Where the line goes.</param>
    /// <param name="command">The command.</param>
    /// <param name="data">The result.</param>
    /// <returns><see cref="Done"/>.</returns>
    public static async Task<int> OkAsync(TextWriter output, string command, object? data)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(new Line(true, command, data, null, null), Options));
        return Done;
    }

    /// <summary>Writes a refusal or failure line.</summary>
    /// <param name="output">Where the line goes.</param>
    /// <param name="command">The command, when known.</param>
    /// <param name="error">The error word, such as <c>usage</c> or <c>not-found</c>.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="exit">The exit code.</param>
    /// <param name="data">A partial result, when there is one.</param>
    /// <returns><paramref name="exit"/>.</returns>
    public static async Task<int> ErrorAsync(TextWriter output, string? command, string error, string message, int exit = Refused, object? data = null)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(new Line(false, command, data, error, message), Options));
        return exit;
    }

    /// <summary>Writes an error of the contract as a refusal line.</summary>
    /// <param name="output">Where the line goes.</param>
    /// <param name="command">The command.</param>
    /// <param name="error">The contract's error.</param>
    /// <returns><see cref="Refused"/>.</returns>
    public static Task<int> ErrorAsync(TextWriter output, string command, AppError error) =>
        ErrorAsync(output, command, Word(error.Type), error.Message);

    /// <summary>Turns an error type into its word: <c>NotFound</c> reads <c>not-found</c>, <c>Validation</c> reads <c>invalid</c>.</summary>
    /// <param name="type">The error type.</param>
    public static string Word(AppErrorType type) =>
        type == AppErrorType.Validation ? "invalid" : JsonNamingPolicy.KebabCaseLower.ConvertName(type.ToString());

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    /// <summary>Represents one output line.</summary>
    private sealed record Line(bool Ok, string? Command, object? Data, string? Error, string? Message);
}
