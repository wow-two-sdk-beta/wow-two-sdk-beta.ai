using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Writes and reads the contract's enumerations as the store's text labels: snake_case member names
/// (<c>schema_invalid</c>, <c>not_configured</c>), as the migrations' check constraints list them.</summary>
internal static class AiLabels
{
    /// <summary>Writes a member as its label.</summary>
    /// <typeparam name="TEnum">The enumeration.</typeparam>
    /// <param name="value">The member.</param>
    public static string Write<TEnum>(TEnum value) where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>Reads a label back, failing loudly on one the module never wrote.</summary>
    /// <typeparam name="TEnum">The enumeration.</typeparam>
    /// <param name="label">The label.</param>
    public static TEnum Read<TEnum>(string label) where TEnum : struct, Enum =>
        TryRead<TEnum>(label, out var value) ? value : throw new InvalidOperationException($"'{label}' is not a {typeof(TEnum).Name} label.");

    /// <summary>Reads a label or a member name, ignoring case and separators.</summary>
    /// <typeparam name="TEnum">The enumeration.</typeparam>
    /// <param name="label">The label, such as <c>schema_invalid</c> or <c>SchemaInvalid</c>.</param>
    /// <param name="value">The member.</param>
    public static bool TryRead<TEnum>(string? label, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        var compact = label.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        foreach (var member in Enum.GetValues<TEnum>())
        {
            if (string.Equals(member.ToString(), compact, StringComparison.OrdinalIgnoreCase))
            {
                value = member;
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes an outcome.</summary>
    public static string Outcome(AiOutcome outcome) => Write(outcome);

    /// <summary>Writes an error kind, when there is one.</summary>
    public static string? ErrorKind(AiErrorKind? kind) => kind is { } value ? Write(value) : null;

    /// <summary>Writes a prompt mode.</summary>
    public static string Mode(PromptMode mode) => Write(mode);
}
