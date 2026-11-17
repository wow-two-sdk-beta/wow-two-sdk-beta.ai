namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Describes the named providers: which are configured, and which models each one serves right now.</summary>
/// <remarks>Never reveals a key: a provider reads as its name, kind, whether it is configured and its base URL's host.</remarks>
public interface IAiProviderCatalog
{
    /// <summary>Lists every provider under <see cref="AiProvidersOptions.SectionName"/>, by name.</summary>
    /// <returns>Each provider's name, kind, whether it is configured, and its base URL's host.</returns>
    IReadOnlyList<AiProviderInfo> List();

    /// <summary>Asks a provider for the model ids it serves now: Anthropic's and OpenAI's models endpoints through their
    /// SDK clients, Ollama's local tags.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="cancellationToken">The token that cancels the call; a cancellation by it throws.</param>
    /// <returns>The model ids, or a classified error: an unknown or unconfigured provider answers
    /// <see cref="Runs.AiErrorKind.NotConfigured"/> without a call; a provider failure never throws.</returns>
    Task<AiProviderModels> ListModelsAsync(string provider, CancellationToken cancellationToken = default);
}
