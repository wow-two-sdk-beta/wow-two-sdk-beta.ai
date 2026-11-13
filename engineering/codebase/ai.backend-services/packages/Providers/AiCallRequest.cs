namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Represents one model attempt: rendered messages, the answer's schema and the call parameters.</summary>
public sealed record AiCallRequest
{
    /// <summary>Gets the model to call.</summary>
    public required AiModelRef Model { get; init; }

    /// <summary>Gets the rendered system message.</summary>
    public required string System { get; init; }

    /// <summary>Gets the rendered user message.</summary>
    public required string User { get; init; }

    /// <summary>Gets the answer's JSON schema as text (an object schema); none takes free text.</summary>
    public string? JsonSchema { get; init; }

    /// <summary>Gets the schema's name, as providers require one; the use case key.</summary>
    public string SchemaName { get; init; } = "answer";

    /// <summary>Gets the sampling temperature; none leaves the provider's default.</summary>
    public double? Temperature { get; init; }

    /// <summary>Gets the most output tokens.</summary>
    public int MaxOutputTokens { get; init; } = 1024;

    /// <summary>Gets the reasoning effort, <c>none</c>, <c>low</c>, <c>medium</c> or <c>high</c>; none leaves the provider's default.</summary>
    public string? Effort { get; init; }

    /// <summary>Gets whether to mark the system message cacheable where the provider supports it.</summary>
    public bool CacheSystem { get; init; } = true;
}
