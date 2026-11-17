using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Reads and writes data scopes: the store keeps <see cref="AiConstants.Unscoped"/> (<c>''</c>) for the unscoped
/// bucket, a model reads it back as none.</summary>
internal static class AiScopes
{
    /// <summary>Writes a scope as the store keeps it: trimmed, <see cref="AiConstants.Unscoped"/> for none or blank.</summary>
    /// <param name="scope">The scope.</param>
    public static string Store(string? scope) => string.IsNullOrWhiteSpace(scope) ? AiConstants.Unscoped : scope.Trim();

    /// <summary>Reads a stored scope back: none for the unscoped bucket.</summary>
    /// <param name="stored">The stored scope.</param>
    public static string? Read(string stored) => string.IsNullOrEmpty(stored) ? null : stored;

    /// <summary>Checks a scope a caller gives: at most <see cref="AiConstants.MaxScopeLength"/> characters, none of them
    /// a control character.</summary>
    /// <param name="scope">The scope; none or blank names the unscoped bucket.</param>
    /// <returns>The error, or none when the scope is fine.</returns>
    public static AppError? Check(string? scope)
    {
        var stored = Store(scope);
        if (stored.Length > AiConstants.MaxScopeLength)
            return AiErrors.Invalid($"A data scope is at most {AiConstants.MaxScopeLength} characters.");
        return stored.Any(char.IsControl) ? AiErrors.Invalid("A data scope holds no control characters.") : null;
    }

    /// <summary>Normalizes a filter's scopes as the store keeps them; none stays none (every scope).</summary>
    /// <param name="scopes">The scopes to keep.</param>
    public static string[]? Filter(IReadOnlyList<string>? scopes) =>
        scopes is null ? null : [.. scopes.Select(Store).Distinct(StringComparer.Ordinal)];
}
