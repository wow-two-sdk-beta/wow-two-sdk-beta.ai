using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Keeps model prices on the Ai service.</summary>
/// <param name="transport">The API transport.</param>
internal sealed class ApiModelPricesBroker(AiApiTransport transport) : IModelPricesBroker
{
    /// <inheritdoc />
    public Task<Result<ModelPriceModel>> PutAsync(ModelPriceModel price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);
        return transport.SendAsync<ModelPriceModel>(HttpMethod.Put, AiRouteConstants.Prices, price, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ModelPriceModel>>> ListAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<IReadOnlyList<ModelPriceModel>>(AiRouteConstants.Prices, cancellationToken);
}
