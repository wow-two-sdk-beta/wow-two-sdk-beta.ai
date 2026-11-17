using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Builds a provider's raw chat client for one model; the seam tests replace.</summary>
internal interface IAiInnerChatClientFactory
{
    /// <summary>Creates the raw client for <paramref name="model"/> on <paramref name="provider"/>.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="kind">The resolved kind (<see cref="AiProviderKinds"/>).</param>
    /// <param name="options">The provider's settings; a key is present unless the kind is Ollama.</param>
    /// <param name="model">The provider's model id.</param>
    IChatClient Create(string provider, string kind, AiProviderOptions options, string model);
}

/// <summary>Lists the model ids a provider serves now; the seam tests replace.</summary>
internal interface IAiModelLister
{
    /// <summary>Asks <paramref name="provider"/> for its model ids.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="kind">The resolved kind (<see cref="AiProviderKinds"/>).</param>
    /// <param name="options">The provider's settings; a key is present unless the kind is Ollama.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The model ids, as the provider names them.</returns>
    Task<IReadOnlyList<string>> ListModelsAsync(string provider, string kind, AiProviderOptions options, CancellationToken cancellationToken);
}

/// <summary>Builds the SDK clients: one SDK client per provider, one chat client per model. Every SDK runs a single
/// attempt (no retries of its own) under the provider's timeout; the module owns retries. The same SDK clients list a
/// provider's models.</summary>
/// <param name="handlerFactory">Supplies the HTTP handler every SDK sends through; none takes each SDK's own. Tests
/// pass a stub to check the wire format without a network.</param>
internal sealed class AiInnerChatClientFactory(Func<HttpMessageHandler>? handlerFactory = null) : IAiInnerChatClientFactory, IAiModelLister, IDisposable
{
    /// <summary>OpenAI's public endpoint, set explicitly so no environment variable redirects the provider.</summary>
    internal const string OpenAiDefaultBaseUrl = "https://api.openai.com/v1";

    private readonly ConcurrentDictionary<string, object> providerClients = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IChatClient Create(string provider, string kind, AiProviderOptions options, string model)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        return kind switch
        {
            AiProviderKinds.Anthropic => ((IAnthropicClient)providerClients.GetOrAdd(provider, _ => CreateAnthropic(options, timeout)))
                .AsIChatClient(model),
            AiProviderKinds.OpenAi => ((OpenAIClient)providerClients.GetOrAdd(provider, _ => CreateOpenAi(options, timeout)))
                .GetChatClient(model).AsIChatClient(),
            AiProviderKinds.Ollama => new OllamaApiClient(
                (HttpClient)providerClients.GetOrAdd(provider, _ => CreateOllamaHttp(options, timeout)), model),
            _ => throw new InvalidOperationException($"The provider '{provider}' has the unknown kind '{kind}'."),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListModelsAsync(string provider, string kind, AiProviderOptions options, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        switch (kind)
        {
            case AiProviderKinds.Anthropic:
            {
                var client = (IAnthropicClient)providerClients.GetOrAdd(provider, _ => CreateAnthropic(options, timeout));
                var ids = new List<string>();
                var page = await client.Models.List(cancellationToken: cancellationToken).ConfigureAwait(false);
                while (true)
                {
                    ids.AddRange(page.Items.Select(model => model.ID));
                    if (!page.HasNext())
                        return ids;
                    page = await page.Next(cancellationToken).ConfigureAwait(false);
                }
            }
            case AiProviderKinds.OpenAi:
            {
                var client = (OpenAIClient)providerClients.GetOrAdd(provider, _ => CreateOpenAi(options, timeout));
                var models = await client.GetOpenAIModelClient().GetModelsAsync(cancellationToken).ConfigureAwait(false);
                return [.. models.Value.Select(model => model.Id)];
            }
            case AiProviderKinds.Ollama:
            {
                var http = (HttpClient)providerClients.GetOrAdd(provider, _ => CreateOllamaHttp(options, timeout));
                // Not disposed: the HTTP client is the provider's, shared with its chat clients.
                var client = new OllamaApiClient(http);
                var models = await client.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);
                return [.. models.Select(model => model.Name)];
            }
            default:
                throw new InvalidOperationException($"The provider '{provider}' has the unknown kind '{kind}'.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var client in providerClients.Values)
            (client as IDisposable)?.Dispose();
        providerClients.Clear();
    }

    private AnthropicClient CreateAnthropic(AiProviderOptions options, TimeSpan timeout)
    {
        var clientOptions = new ClientOptions
        {
            ApiKey = options.ApiKey,
            MaxRetries = 0,
            Timeout = timeout,
            // Set always, so the SDK's ANTHROPIC_BASE_URL fallback never redirects a configured provider.
            BaseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? EnvironmentUrl.Production : options.BaseUrl.TrimEnd('/'),
        };
        if (handlerFactory is not null)
            clientOptions.HttpClient = new HttpClient(handlerFactory()) { Timeout = Timeout.InfiniteTimeSpan };
        return new AnthropicClient(clientOptions);
    }

    private OpenAIClient CreateOpenAi(AiProviderOptions options, TimeSpan timeout)
    {
        var clientOptions = new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = timeout,
            Endpoint = new Uri(string.IsNullOrWhiteSpace(options.BaseUrl) ? OpenAiDefaultBaseUrl : options.BaseUrl.TrimEnd('/')),
        };
        if (handlerFactory is not null)
            clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handlerFactory()) { Timeout = Timeout.InfiniteTimeSpan });
        return new OpenAIClient(new ApiKeyCredential(options.ApiKey!), clientOptions);
    }

    private HttpClient CreateOllamaHttp(AiProviderOptions options, TimeSpan timeout)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? AiProviderKinds.OllamaDefaultBaseUrl : options.BaseUrl;
        var http = handlerFactory is null ? new HttpClient() : new HttpClient(handlerFactory());
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        http.Timeout = timeout;
        return http;
    }
}
