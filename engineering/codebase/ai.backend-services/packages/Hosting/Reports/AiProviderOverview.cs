using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Reports;

/// <summary>Represents one provider as <c>GET providers</c> and <c>ai providers</c> show it: the catalog's entry, the models
/// it has a price for, and, on request, the models it serves now. Never its key.</summary>
public sealed record AiProviderSummary
{
    /// <summary>Gets the provider name, the first part of a model reference.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the resolved kind; none when the settings name no known kind or only a price names the provider.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets whether calls can reach the provider.</summary>
    public bool Configured { get; init; }

    /// <summary>Gets the host calls go to; never a path or query.</summary>
    public string? BaseUrlHost { get; init; }

    /// <summary>Gets why the provider cannot be called, when it cannot.</summary>
    public string? Problem { get; init; }

    /// <summary>Gets the model ids with a price row, sorted.</summary>
    public IReadOnlyList<string> PricedModels { get; init; } = [];

    /// <summary>Gets the model ids the provider served when asked; none unless live ids were asked for or when the
    /// listing failed.</summary>
    public IReadOnlyList<string>? LiveModels { get; init; }

    /// <summary>Gets why the live listing failed, when it did.</summary>
    public AiErrorKind? LiveErrorKind { get; init; }

    /// <summary>Gets the provider's message, key-free, when the live listing failed.</summary>
    public string? LiveError { get; init; }
}

/// <summary>Builds the provider overview: every configured provider and every provider a price names, by name.</summary>
public static class AiProviderOverview
{
    /// <summary>Builds the overview.</summary>
    /// <param name="catalog">The configured providers; none lists the priced ones only.</param>
    /// <param name="prices">The price rows.</param>
    /// <param name="live">Whether to ask each configured provider for its models now.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public static async Task<IReadOnlyList<AiProviderSummary>> BuildAsync(IAiProviderCatalog? catalog, IModelPricesBroker prices, bool live,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prices);
        var priced = await prices.ListAsync(cancellationToken) is Result<IReadOnlyList<ModelPriceModel>>.Success { Value: var rows }
            ? rows.GroupBy(row => row.Provider, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)[.. group.Select(row => row.Model).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], StringComparer.Ordinal)
            : [];

        var configured = catalog?.List() ?? [];
        var summaries = new List<AiProviderSummary>();
        foreach (var provider in configured)
        {
            var summary = new AiProviderSummary
            {
                Name = provider.Name,
                Kind = provider.Kind,
                Configured = provider.Configured,
                BaseUrlHost = provider.BaseUrlHost,
                Problem = provider.Problem,
                PricedModels = priced.GetValueOrDefault(provider.Name) ?? [],
            };
            if (live && catalog is not null && provider.Configured)
            {
                var models = await catalog.ListModelsAsync(provider.Name, cancellationToken);
                summary = summary with
                {
                    LiveModels = models.Succeeded ? models.Models : null,
                    LiveErrorKind = models.ErrorKind,
                    LiveError = models.Error,
                };
            }

            summaries.Add(summary);
        }

        foreach (var (name, models) in priced.Where(pair => configured.All(provider => provider.Name != pair.Key)))
        {
            summaries.Add(new AiProviderSummary
            {
                Name = name,
                PricedModels = models,
                Problem = $"No provider '{name}' is configured; set Ai:Providers:{name}.",
            });
        }

        return [.. summaries.OrderBy(summary => summary.Name, StringComparer.Ordinal)];
    }
}
