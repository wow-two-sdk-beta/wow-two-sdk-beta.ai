using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Validates the keys the module stores: use cases, eval sets and labels.</summary>
internal static partial class AiKeys
{
    /// <summary>Gets whether <paramref name="key"/> is lowercase letters, digits and hyphens, 1 to 64 characters.</summary>
    /// <param name="key">The key.</param>
    public static bool IsValid(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>Describes a rejected key.</summary>
    /// <param name="what">What the key names, such as <c>use case</c>.</param>
    /// <param name="key">The key.</param>
    public static string Rejection(string what, string? key) =>
        $"The {what} key '{key}' must be 1 to {AiConstants.MaxKeyLength} lowercase letters, digits or hyphens.";

    [GeneratedRegex("^[a-z0-9-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
