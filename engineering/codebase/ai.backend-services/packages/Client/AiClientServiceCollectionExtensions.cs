using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client;

/// <summary>Registers the Ai HTTP client (Forms 2 and 3).</summary>
/// <remarks>The entry point Forms 2 and 3 compose: it binds the service's root URL and the shared named
/// <see cref="System.Net.Http.HttpClient"/>. It registers no broker yet — the HTTP brokers land with the service's
/// controllers (backlog); until then a product hosts the module in process (<c>AddAi</c>).</remarks>
public static class AiClientServiceCollectionExtensions
{
    /// <summary>Registers the client's options and its named HTTP client over the running Ai service.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the service's root URL.</param>
    /// <returns>The HTTP client builder, for resilience handlers or a test server's handler.</returns>
    public static IHttpClientBuilder AddAiClient(this IServiceCollection services, Action<AiClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<AiClientOptions>()
            .Configure(configure)
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "AiClientOptions.BaseAddress must be an absolute URL.");

        return services.AddHttpClient(AiClientConstants.HttpClientName, (provider, http) =>
        {
            var root = provider.GetRequiredService<IOptions<AiClientOptions>>().Value.BaseAddress!.ToString();
            http.BaseAddress = new Uri(root.EndsWith('/') ? root : root + "/");
        });
    }
}
