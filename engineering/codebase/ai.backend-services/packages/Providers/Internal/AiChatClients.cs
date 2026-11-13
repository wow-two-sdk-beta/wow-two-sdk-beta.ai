using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Caches one OpenTelemetry-wrapped chat client per provider and model, built by the inner factory.</summary>
internal sealed class AiChatClients(
    IOptions<AiProvidersOptions> options,
    IAiInnerChatClientFactory innerFactory,
    ILoggerFactory? loggerFactory = null) : IAiChatClients, IDisposable
{
    // Keyed by provider (any case) and model (exact case: some hosts' model ids are case-sensitive).
    private readonly ConcurrentDictionary<(string Provider, string Model), Lazy<IChatClient>> clients = new();

    /// <inheritdoc />
    public bool IsConfigured(string provider) => AiProviderResolution.Describe(options.Value, provider).Problem is null;

    /// <inheritdoc />
    public IChatClient Get(AiModelRef model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var (kind, settings, problem) = AiProviderResolution.Describe(options.Value, model.Provider);
        if (problem is not null)
            throw new InvalidOperationException(problem);

        var lazy = clients.GetOrAdd((model.Provider.ToLowerInvariant(), model.Model), _ => new Lazy<IChatClient>(() =>
            new ChatClientBuilder(innerFactory.Create(model.Provider, kind!, settings!, model.Model))
                .UseOpenTelemetry(loggerFactory)
                .Build()));
        return lazy.Value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var client in clients.Values.Where(lazy => lazy.IsValueCreated))
            client.Value.Dispose();
        clients.Clear();
    }
}
