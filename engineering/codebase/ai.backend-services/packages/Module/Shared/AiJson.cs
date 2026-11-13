using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Reads and writes the module's jsonb values: prompt parameters, eval inputs and answers.</summary>
internal static class AiJson
{
    /// <summary>Gets the options stored JSON is written and read with: camelCase names, nulls left out.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Writes prompt parameters as stored: camelCase, nulls left out.</summary>
    /// <param name="parameters">The parameters.</param>
    public static string WriteParameters(PromptParameters parameters) => JsonSerializer.Serialize(parameters, Options);

    /// <summary>Reads stored prompt parameters.</summary>
    /// <param name="json">The stored JSON.</param>
    public static PromptParameters ReadParameters(string json) => JsonSerializer.Deserialize<PromptParameters>(json, Options) ?? new PromptParameters();

    /// <summary>Writes prompt parameters canonically for hashing: keys in ordinal order, nulls left out, no whitespace.</summary>
    /// <param name="parameters">The parameters.</param>
    public static string CanonicalParameters(PromptParameters parameters)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (parameters.Effort is { } effort)
                writer.WriteString("effort", effort);
            if (parameters.MaxOutputTokens is { } maxOutputTokens)
                writer.WriteNumber("maxOutputTokens", maxOutputTokens);
            if (parameters.Temperature is { } temperature)
                writer.WriteNumber("temperature", temperature);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Writes prompt variables as a JSON object of strings.</summary>
    /// <param name="variables">The variables.</param>
    public static string WriteVariables(IReadOnlyDictionary<string, string?> variables) =>
        JsonSerializer.Serialize(new SortedDictionary<string, string?>(variables.ToDictionary(), StringComparer.Ordinal), Options);

    /// <summary>Reads prompt variables: string values as they are, other values as their JSON text, null as null.</summary>
    /// <param name="json">A JSON object.</param>
    public static IReadOnlyDictionary<string, string?> ReadVariables(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ToVariables(document.RootElement);
    }

    /// <summary>Turns a JSON object into prompt variables: string values as they are, other values as their JSON text.</summary>
    /// <param name="element">A JSON object.</param>
    /// <exception cref="JsonException">The element is not an object.</exception>
    public static IReadOnlyDictionary<string, string?> ToVariables(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("Prompt variables must be a JSON object.");

        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            variables[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText(),
            };
        }

        return variables;
    }

    /// <summary>Gets the writer settings every compact JSON text is written with: non-ASCII text (Cyrillic posts) unescaped.</summary>
    public static JsonWriterOptions WriterOptions { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Normalizes a JSON object's text: parsed and written back compactly; none when it is not a JSON object.</summary>
    /// <param name="json">The text.</param>
    public static string? NormalizeObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Write(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes a stored jsonb value back compactly, as the module wrote it (PostgreSQL prints jsonb with spaces).</summary>
    /// <param name="json">The stored text.</param>
    public static string Compact(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Write(document.RootElement);
    }

    /// <summary>Writes a stored jsonb value back compactly; none stays none.</summary>
    /// <param name="json">The stored text.</param>
    public static string? CompactOrNull(string? json) => json is null ? null : Compact(json);

    /// <summary>Writes an element compactly with <see cref="WriterOptions"/>.</summary>
    /// <param name="element">The element.</param>
    public static string Write(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
            element.WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
