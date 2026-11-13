using Microsoft.Extensions.AI;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Hands out the configured providers' chat clients, one per provider and model, wrapped with OpenTelemetry.</summary>
public interface IAiChatClients
{
    /// <summary>Gets whether <paramref name="provider"/> is configured: a known kind, with a key unless it is Ollama.</summary>
    /// <param name="provider">The provider name.</param>
    bool IsConfigured(string provider);

    /// <summary>Gets the chat client for <paramref name="model"/>; the same instance on every call.</summary>
    /// <param name="model">The model reference.</param>
    /// <returns>The client; it defaults <see cref="ChatOptions.ModelId"/> to the reference's model. The factory owns it.</returns>
    /// <exception cref="InvalidOperationException">The provider is missing, of an unknown kind or without its key.</exception>
    IChatClient Get(AiModelRef model);
}
