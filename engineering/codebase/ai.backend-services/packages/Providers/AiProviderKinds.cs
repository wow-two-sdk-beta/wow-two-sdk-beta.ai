namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Names the provider kinds <see cref="AiProviderOptions.Kind"/> takes.</summary>
public static class AiProviderKinds
{
    /// <summary>Anthropic's Messages API, through the official <c>Anthropic</c> SDK.</summary>
    public const string Anthropic = "anthropic";

    /// <summary>Any OpenAI-compatible Chat Completions host (OpenAI, Groq, DeepSeek, Ollama's <c>/v1</c>).</summary>
    public const string OpenAi = "openai";

    /// <summary>Ollama's native API, through OllamaSharp; needs no key.</summary>
    public const string Ollama = "ollama";

    /// <summary>Ollama's default base URL.</summary>
    public const string OllamaDefaultBaseUrl = "http://localhost:11434";

    /// <summary>Resolves a provider's kind: its <see cref="AiProviderOptions.Kind"/>, else its name when the name is a kind.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="options">The provider's settings.</param>
    /// <returns>The kind in lower case, or none when neither names a known kind.</returns>
    public static string? Resolve(string provider, AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var kind = string.IsNullOrWhiteSpace(options.Kind) ? provider : options.Kind;
        kind = kind?.Trim().ToLowerInvariant();
        return kind is Anthropic or OpenAi or Ollama ? kind : null;
    }
}
