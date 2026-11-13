using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Registers the named model providers.</summary>
public static class AiProvidersServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IAiModelCaller"/> and <see cref="IAiChatClients"/> (singletons) over the providers
    /// bound from <see cref="AiProvidersOptions.SectionName"/>.</summary>
    /// <remarks>
    ///   - every child of the section is one provider, named by its key: <c>Kind</c>, <c>BaseUrl</c>, <c>ApiKey</c>,
    ///     <c>TimeoutSeconds</c> (<see cref="AiProviderOptions"/>)
    ///   - <paramref name="configure"/> runs after the binding, so code can add a provider or override a value
    ///   - a provider other than Ollama with no key stays unconfigured: its calls answer
    ///     <see cref="Runs.AiErrorKind.NotConfigured"/> without reaching the network
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration holding <c>Ai:Providers</c>.</param>
    /// <param name="configure">Changes the bound providers in code.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration,
        Action<AiProvidersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = services.AddOptions<AiProvidersOptions>().Configure(options => Bind(options, configuration));
        if (configure is not null)
            builder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAiInnerChatClientFactory>(_ => new AiInnerChatClientFactory());
        services.TryAddSingleton<IAiChatClients>(provider => new AiChatClients(
            provider.GetRequiredService<IOptions<AiProvidersOptions>>(),
            provider.GetRequiredService<IAiInnerChatClientFactory>(),
            provider.GetService<ILoggerFactory>()));
        services.TryAddSingleton<IAiModelCaller>(provider => new AiModelCaller(
            provider.GetRequiredService<IOptions<AiProvidersOptions>>(),
            provider.GetRequiredService<IAiChatClients>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }

    /// <summary>Registers a plain <see cref="IChatClient"/> (singleton) for one provider's model, for a product that only
    /// wants a chat client; <see cref="AddAiProviders"/> must register the providers.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="provider">The configured provider name, such as <c>ollama</c>.</param>
    /// <param name="model">The provider's model id, such as <c>llama3.2</c>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAiChatClient(this IServiceCollection services, string provider, string model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var reference = new AiModelRef(provider.Trim().ToLowerInvariant(), model.Trim());
        // The factory owns the client; the wrapper keeps the container from disposing it a second time.
        services.AddSingleton<IChatClient>(serviceProvider =>
            new FactoryOwnedChatClient(serviceProvider.GetRequiredService<IAiChatClients>().Get(reference)));
        return services;
    }

    /// <summary>Binds every child of <c>Ai:Providers</c> as one named provider.</summary>
    /// <param name="options">The options to fill.</param>
    /// <param name="configuration">The configuration root.</param>
    internal static void Bind(AiProvidersOptions options, IConfiguration configuration)
    {
        foreach (var child in configuration.GetSection(AiProvidersOptions.SectionName).GetChildren())
        {
            if (!options.Providers.TryGetValue(child.Key, out var provider))
                options.Providers[child.Key] = provider = new AiProviderOptions();
            child.Bind(provider);
        }
    }

    /// <summary>Passes every call through and leaves disposal to <see cref="IAiChatClients"/>.</summary>
    /// <param name="inner">The factory's client.</param>
    private sealed class FactoryOwnedChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }
}
