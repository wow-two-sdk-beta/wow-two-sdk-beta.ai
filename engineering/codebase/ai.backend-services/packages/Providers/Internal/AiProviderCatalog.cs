using Anthropic.Core;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

/// <summary>Describes the configured providers and asks them for their live model ids, never throwing for a provider
/// failure and never revealing a key.</summary>
/// <param name="options">Every configured provider.</param>
/// <param name="lister">Asks a provider's SDK client for its models.</param>
internal sealed class AiProviderCatalog(IOptions<AiProvidersOptions> options, IAiModelLister lister) : IAiProviderCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<AiProviderInfo> List() =>
    [
        .. options.Value.Providers
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair =>
            {
                var name = pair.Key.Trim().ToLowerInvariant();
                var (kind, settings, problem) = AiProviderResolution.Describe(options.Value, pair.Key);
                return new AiProviderInfo
                {
                    Name = name,
                    Kind = kind,
                    Configured = problem is null,
                    BaseUrlHost = HostOf(kind, settings),
                    Problem = problem,
                };
            }),
    ];

    /// <inheritdoc />
    public async Task<AiProviderModels> ListModelsAsync(string provider, CancellationToken cancellationToken = default)
    {
        var name = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        var (kind, settings, problem) = AiProviderResolution.Describe(options.Value, name);
        if (problem is not null)
            return Failed(name, AiErrorKind.NotConfigured, problem);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings!.TimeoutSeconds)));
        try
        {
            var models = await lister.ListModelsAsync(name, kind!, settings, timeout.Token).ConfigureAwait(false);
            return new AiProviderModels
            {
                Provider = name,
                Models = [.. models.Where(model => !string.IsNullOrWhiteSpace(model)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The caller cancelled the model listing.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            return Failed(name, AiErrorClassifier.Classify(exception),
                AiErrorClassifier.Sanitize(AiErrorClassifier.Describe(exception), settings.ApiKey));
        }
    }

    /// <summary>Gets the host calls go to: the base URL's, else the kind's public endpoint's; never a path or query.</summary>
    /// <param name="kind">The resolved kind, when known.</param>
    /// <param name="settings">The provider's settings, when present.</param>
    internal static string? HostOf(string? kind, AiProviderOptions? settings)
    {
        var url = settings?.BaseUrl is { Length: > 0 } configured ? configured : kind switch
        {
            AiProviderKinds.Anthropic => EnvironmentUrl.Production,
            AiProviderKinds.OpenAi => AiInnerChatClientFactory.OpenAiDefaultBaseUrl,
            AiProviderKinds.Ollama => AiProviderKinds.OllamaDefaultBaseUrl,
            _ => null,
        };
        return url is not null && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static AiProviderModels Failed(string provider, AiErrorKind kind, string error) =>
        new() { Provider = provider, ErrorKind = kind, Error = error };
}
