namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Holds one named provider's settings, bound from <c>Ai:Providers:{name}</c>.</summary>
public sealed class AiProviderOptions
{
    /// <summary>Gets or sets the kind: <c>anthropic</c>, <c>openai</c> (any OpenAI-compatible host) or <c>ollama</c>;
    /// empty takes the provider's name when it is one of these.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the base URL; empty takes the kind's public endpoint (Ollama: <c>http://localhost:11434</c>).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the API key; a provider other than Ollama with no key stays unconfigured.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the per-attempt timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Holds every named provider, bound from <c>Ai:Providers</c>.</summary>
public sealed class AiProvidersOptions
{
    /// <summary>The configuration section the providers bind from.</summary>
    public const string SectionName = "Ai:Providers";

    /// <summary>Gets the providers by name, compared case-insensitively.</summary>
    public Dictionary<string, AiProviderOptions> Providers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
