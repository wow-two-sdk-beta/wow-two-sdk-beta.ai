using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;

/// <summary>Checks a model's answer against a use case's output schema, at the top level only: the answer is a JSON object
/// (a surrounding <c>```json</c> fence stripped), every <c>required</c> property is present, and each described property
/// keeps its <c>enum</c> values and primitive <c>type</c>s.</summary>
/// <remarks>No third-party validator: nested objects, formats, patterns and ranges are left to the scorer and the
/// provider's own structured output.</remarks>
internal static partial class OutputSchemaCheck
{
    /// <summary>Strips one surrounding Markdown code fence, with or without a language, and the whitespace around it.</summary>
    /// <param name="answer">The answer text.</param>
    public static string StripFence(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var trimmed = answer.Trim();
        var fenced = FencePattern().Match(trimmed);
        return fenced.Success ? fenced.Groups["body"].Value.Trim() : trimmed;
    }

    /// <summary>Checks an answer against a schema.</summary>
    /// <param name="answer">The answer text.</param>
    /// <param name="schema">The schema, a JSON object.</param>
    /// <param name="json">The answer's JSON text, fence stripped, when it passes.</param>
    /// <param name="error">Why it fails, when it does.</param>
    /// <returns>Whether the answer passes.</returns>
    public static bool TryCheck(string answer, string schema, out string json, out string? error)
    {
        json = StripFence(answer);
        error = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            error = $"The answer is not JSON: {exception.Message}";
            return false;
        }

        using (document)
        using (var schemaDocument = JsonDocument.Parse(schema))
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = $"The answer is a JSON {Kind(root.ValueKind)}, not an object.";
                return false;
            }

            var rules = schemaDocument.RootElement;
            if (rules.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            {
                var missing = required.EnumerateArray()
                    .Where(name => name.ValueKind == JsonValueKind.String && !root.TryGetProperty(name.GetString()!, out _))
                    .Select(name => name.GetString())
                    .ToList();
                if (missing.Count > 0)
                {
                    error = $"The answer lacks required {(missing.Count == 1 ? "property" : "properties")} {string.Join(", ", missing)}.";
                    return false;
                }
            }

            if (rules.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (!root.TryGetProperty(property.Name, out var value) || property.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    if (CheckProperty(property.Name, value, property.Value) is { } problem)
                    {
                        error = problem;
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Checks one property's value against its schema's <c>type</c> and <c>enum</c>.</summary>
    private static string? CheckProperty(string name, JsonElement value, JsonElement rules)
    {
        if (rules.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind switch
            {
                JsonValueKind.String => [type.GetString()!],
                JsonValueKind.Array => type.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray(),
                _ => Array.Empty<string>(),
            };
            if (types.Length > 0 && !types.Any(expected => IsType(value, expected)))
                return $"The answer's '{name}' is a JSON {Kind(value.ValueKind)}, not {string.Join(" or ", types)}.";
        }

        if (rules.TryGetProperty("enum", out var allowed) && allowed.ValueKind == JsonValueKind.Array
            && !allowed.EnumerateArray().Any(option => SameValue(option, value)))
            return $"The answer's '{name}' is {value.GetRawText()}, not one of {allowed.GetRawText()}.";

        return null;
    }

    /// <summary>Gets whether a value is of a JSON schema primitive type; an unknown type name passes.</summary>
    private static bool IsType(JsonElement value, string type) => type switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && IsInteger(value),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        _ => true,
    };

    private static bool IsInteger(JsonElement value) =>
        value.TryGetInt64(out _) || (decimal.TryParse(value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == decimal.Truncate(number));

    /// <summary>Compares two JSON values: strings by their text, every other value by its raw JSON.</summary>
    private static bool SameValue(JsonElement left, JsonElement right) =>
        left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String
            ? string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal)
            : left.ValueKind == right.ValueKind && string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

    private static string Kind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => kind.ToString().ToLowerInvariant(),
    };

    [GeneratedRegex(@"^```[A-Za-z0-9_-]*[ \t]*\r?\n?(?<body>[\s\S]*?)\r?\n?```$", RegexOptions.CultureInvariant)]
    private static partial Regex FencePattern();
}
