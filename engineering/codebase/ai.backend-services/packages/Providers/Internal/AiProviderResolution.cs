namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Resolves a named provider's kind and settings, or says why it cannot be called.</summary>
internal static class AiProviderResolution
{
    /// <summary>Resolves <paramref name="provider"/> against <paramref name="options"/>.</summary>
    /// <param name="options">Every configured provider.</param>
    /// <param name="provider">The provider name.</param>
    /// <returns>The kind and settings, or the problem; a problem never quotes the key.</returns>
    internal static (string? Kind, AiProviderOptions? Settings, string? Problem) Describe(AiProvidersOptions options, string provider)
    {
        if (string.IsNullOrWhiteSpace(provider) || !options.Providers.TryGetValue(provider, out var settings))
            return (null, null, $"The provider '{provider}' is not configured under {AiProvidersOptions.SectionName}.");
        var kind = AiProviderKinds.Resolve(provider, settings);
        if (kind is null)
            return (null, settings, $"The provider '{provider}' has no known kind ('{settings.Kind}'); use anthropic, openai or ollama.");
        if (kind != AiProviderKinds.Ollama && string.IsNullOrWhiteSpace(settings.ApiKey))
            return (kind, settings, $"The provider '{provider}' has no API key.");
        return (kind, settings, null);
    }
}
