namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Represents one prompt row: a use case's text for every model, or for the models its match names.</summary>
public sealed record PromptModel
{
    /// <summary>Gets the row id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the models the row serves: none for every model, an exact model id, or a prefix ending in <c>*</c>
    /// (<c>claude-*</c>); the most specific match wins.</summary>
    public string? ModelMatch { get; init; }

    /// <summary>Gets the version: the next number on each versioned save; unchanged by a rewrite.</summary>
    public required int Version { get; init; }

    /// <summary>Gets the system message, with <c>{{name}}</c> placeholders.</summary>
    public required string SystemTemplate { get; init; }

    /// <summary>Gets the user message, with <c>{{name}}</c> placeholders.</summary>
    public required string UserTemplate { get; init; }

    /// <summary>Gets the call parameters.</summary>
    public PromptParameters Parameters { get; init; } = new();

    /// <summary>Gets the SHA-256 of the templates and parameters, first 12 hex characters.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Gets why the row was saved.</summary>
    public string? Note { get; init; }

    /// <summary>Gets who saved it first.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Gets when the row was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when the row last changed (UTC); a rewrite moves it.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
