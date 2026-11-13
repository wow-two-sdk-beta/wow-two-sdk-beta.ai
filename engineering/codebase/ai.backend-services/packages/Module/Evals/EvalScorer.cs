using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Scores one answer against an item's expected answer: the set's score fields (or every field the expected
/// answer names) compared as text, case-insensitively.</summary>
/// <remarks>
///   - a string compares by its value, any other value by its raw JSON (<c>true</c>, <c>3</c>, <c>["a"]</c>); a field the
///     answer lacks reads <c>null</c>; a score field the expected answer lacks is not compared
///   - each miss reads <c>{field}:{expected}&gt;{answer}</c>; the item is severe when any miss is one of the set's severe
///     misses (compared case-insensitively)
///   - an answer that is not a JSON object misses every compared field
/// </remarks>
internal static class EvalScorer
{
    /// <summary>Scores an answer.</summary>
    /// <param name="expected">The expected answer, a JSON object.</param>
    /// <param name="answer">The model's answer text.</param>
    /// <param name="scoreFields">The fields to compare; none compares every expected field.</param>
    /// <param name="severeMisses">The misses counted as severe.</param>
    public static EvalScore Score(string expected, string? answer, IReadOnlyList<string> scoreFields, IReadOnlyList<string> severeMisses)
    {
        ArgumentNullException.ThrowIfNull(expected);
        using var expectedDocument = JsonDocument.Parse(expected);
        using var answerDocument = TryParseObject(answer);
        var expectedRoot = expectedDocument.RootElement;
        var fields = scoreFields.Count > 0
            ? scoreFields
            : expectedRoot.ValueKind == JsonValueKind.Object ? [.. expectedRoot.EnumerateObject().Select(property => property.Name)] : [];

        var misses = new List<string>();
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WoW.Two.Sdk.Beta.Be.Ai.Module.Shared.AiJson.WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var field in fields)
            {
                if (expectedRoot.ValueKind != JsonValueKind.Object || !expectedRoot.TryGetProperty(field, out var want))
                    continue;

                var has = answerDocument is not null && answerDocument.RootElement.TryGetProperty(field, out var got);
                var gotValue = has ? answerDocument!.RootElement.GetProperty(field) : default;
                writer.WritePropertyName(field);
                if (has)
                    gotValue.WriteTo(writer);
                else
                    writer.WriteNullValue();

                var wantText = Text(want);
                var gotText = has ? Text(gotValue) : "null";
                if (!string.Equals(wantText, gotText, StringComparison.OrdinalIgnoreCase))
                    misses.Add($"{field}:{wantText}>{gotText}");
            }

            writer.WriteEndObject();
        }

        var severe = misses.Any(miss => severeMisses.Contains(miss, StringComparer.OrdinalIgnoreCase));
        return new EvalScore(misses.Count == 0, severe, misses, Encoding.UTF8.GetString(buffer.ToArray()));
    }

    /// <summary>Writes a value as the scorer compares it: a string's value, any other value's raw JSON.</summary>
    /// <param name="value">The value.</param>
    public static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static JsonDocument? TryParseObject(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return null;

        try
        {
            var document = JsonDocument.Parse(answer);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return document;

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Represents one scored answer.</summary>
/// <param name="Correct">Whether every compared field matched.</param>
/// <param name="Severe">Whether a miss is severe.</param>
/// <param name="Misses">The misses, each <c>{field}:{expected}&gt;{answer}</c>.</param>
/// <param name="Answer">The compared fields of the answer, as a JSON object.</param>
internal sealed record EvalScore(bool Correct, bool Severe, IReadOnlyList<string> Misses, string Answer);
