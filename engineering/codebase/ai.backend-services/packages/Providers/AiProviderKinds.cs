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

    /// <summary>Gets whether a kind's client sends images: Anthropic as base64 image blocks, OpenAI-compatible hosts as
    /// <c>data:</c> URL image parts, Ollama (OllamaSharp 5.5) as the message's base64 <c>images</c>. A call with images to
    /// any other kind fails as <see cref="Runs.AiErrorKind.BadRequest"/> without reaching the provider; a model that takes
    /// no images answers its provider's own 400, classified the same way.</summary>
    /// <param name="kind">The resolved kind.</param>
    public static bool TakesImages(string kind) => kind is Anthropic or OpenAi or Ollama;
}
