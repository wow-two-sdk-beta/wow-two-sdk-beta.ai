namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

/// <summary>Represents the data scopes one caller may see and write.</summary>
/// <remarks>A scope is written as the store keeps it: <see cref="AiConstants.Unscoped"/> (<c>""</c>) names the unscoped
/// bucket.</remarks>
public sealed record AiScopeGrant
{
    /// <summary>Gets whether the caller sees every scope: an operator.</summary>
    public bool AllScopes { get; init; }

    /// <summary>Gets the scopes the caller sees, when not <see cref="AllScopes"/>.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Gets the scope a write that names none takes; none takes the only granted scope, else the unscoped bucket
    /// when granted.</summary>
    public string? DefaultScope { get; init; }

    /// <summary>Gets the grant of a single-tenant host's caller: the unscoped bucket only.</summary>
    public static AiScopeGrant Unscoped { get; } = new() { Scopes = [AiConstants.Unscoped] };

    /// <summary>Gets the grant of an operator: every scope; a write that names none goes to the unscoped bucket.</summary>
    public static AiScopeGrant Every { get; } = new() { AllScopes = true };

    /// <summary>Gets the grant of no scope at all: every list reads empty and every write is forbidden.</summary>
    public static AiScopeGrant None { get; } = new();

    /// <summary>Creates a grant of the given scopes; the first is where a write that names none goes.</summary>
    /// <param name="scopes">The scopes, <see cref="AiConstants.Unscoped"/> for the unscoped bucket.</param>
    public static AiScopeGrant Of(params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var kept = scopes.Select(Normalize).Distinct(StringComparer.Ordinal).ToArray();
        return new AiScopeGrant { Scopes = kept, DefaultScope = kept.Length > 0 ? kept[0] : null };
    }

    /// <summary>Gets whether the caller sees a scope.</summary>
    /// <param name="scope">The scope; none or blank names the unscoped bucket.</param>
    public bool Allows(string? scope) => AllScopes || Scopes.Select(Normalize).Contains(Normalize(scope), StringComparer.Ordinal);

    /// <summary>Picks the scope a write lands in.</summary>
    /// <param name="requested">The scope the write names; none takes <see cref="DefaultScope"/>, the only granted one, or
    /// the unscoped bucket.</param>
    /// <param name="scope">The scope, as the store keeps it.</param>
    /// <returns>Whether the caller may write there.</returns>
    public bool TryWrite(string? requested, out string scope)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            scope = Normalize(requested);
            return Allows(scope);
        }

        scope = DefaultScope is { } fallback ? Normalize(fallback)
            : !AllScopes && Scopes.Count == 1 ? Normalize(Scopes[0])
            : AiConstants.Unscoped;
        return Allows(scope);
    }

    /// <summary>Narrows a read to the scopes the caller sees.</summary>
    /// <param name="requested">The scopes the read names; none reads every scope the caller sees.</param>
    /// <param name="filter">The scopes to keep; none keeps every scope (an operator naming none).</param>
    /// <returns>Whether the caller sees every scope the read names.</returns>
    public bool TryRead(IReadOnlyList<string>? requested, out IReadOnlyList<string>? filter)
    {
        if (requested is { Count: > 0 })
        {
            var named = requested.Select(Normalize).Distinct(StringComparer.Ordinal).ToArray();
            filter = named;
            return named.All(scope => Allows(scope));
        }

        filter = AllScopes ? null : [.. Scopes.Select(Normalize).Distinct(StringComparer.Ordinal)];
        return true;
    }

    private static string Normalize(string? scope) => string.IsNullOrWhiteSpace(scope) ? AiConstants.Unscoped : scope.Trim();
}
