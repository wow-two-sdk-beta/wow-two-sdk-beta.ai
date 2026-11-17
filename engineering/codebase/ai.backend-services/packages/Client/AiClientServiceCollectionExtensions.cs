using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client;

/// <summary>Registers the Ai HTTP client (Forms 2 and 3).</summary>
/// <remarks>A Form 1 host switches to the running service by swapping registrations: <c>AddAiClient</c> in place of
/// <c>AddAi()</c> and <c>AddAiProviders(configuration)</c>; the product code keeps calling the same brokers.</remarks>
public static class AiClientServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IAiRunner"/>, <see cref="IPromptsBroker"/>, <see cref="IModelPricesBroker"/>,
    /// <see cref="IInferencesBroker"/> and <see cref="IEvalsBroker"/> over the running Ai service's HTTP API
    /// (<see cref="AiRouteConstants"/>), sharing one named <see cref="HttpClient"/>.</summary>
    /// <remarks>
    ///   - the service checks an <c>ai.*</c> scope on every route: add the caller's token through the returned builder
    ///     (<c>AddHttpMessageHandler</c>, an Identity client-credentials handler)
    ///   - the service decides the data scopes the caller sees; a call naming none writes to the caller's default scope
    ///   - a long synchronous eval run may outlast the client's timeout: queue it (<see cref="IEvalsBroker.QueueAsync"/>)
    ///     or raise the timeout through the builder
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the service's root URL.</param>
    /// <returns>The HTTP client builder, for the token handler, resilience handlers or a test server's handler.</returns>
    public static IHttpClientBuilder AddAiClient(this IServiceCollection services, Action<AiClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<AiClientOptions>()
            .Configure(configure)
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "AiClientOptions.BaseAddress must be an absolute URL.");

        services.TryAddTransient(provider => new AiApiTransport(provider.GetRequiredService<IHttpClientFactory>().CreateClient(AiClientConstants.HttpClientName)));
        services.Replace(ServiceDescriptor.Transient<IAiRunner>(provider => new ApiAiRunner(provider.GetRequiredService<AiApiTransport>())));
        services.Replace(ServiceDescriptor.Transient<IPromptsBroker>(provider => new ApiPromptsBroker(provider.GetRequiredService<AiApiTransport>())));
        services.Replace(ServiceDescriptor.Transient<IModelPricesBroker>(provider => new ApiModelPricesBroker(provider.GetRequiredService<AiApiTransport>())));
        services.Replace(ServiceDescriptor.Transient<IInferencesBroker>(provider => new ApiInferencesBroker(provider.GetRequiredService<AiApiTransport>())));
        services.Replace(ServiceDescriptor.Transient<IEvalsBroker>(provider => new ApiEvalsBroker(provider.GetRequiredService<AiApiTransport>())));

        return services.AddHttpClient(AiClientConstants.HttpClientName, (provider, http) =>
        {
            var root = provider.GetRequiredService<IOptions<AiClientOptions>>().Value.BaseAddress!.ToString();
            http.BaseAddress = new Uri(root.EndsWith('/') ? root : root + "/");
        });
    }
}
