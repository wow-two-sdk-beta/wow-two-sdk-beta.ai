using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Prices;

/// <summary>Keeps model prices with their history; the runner prices each attempt from the row in effect.</summary>
public interface IModelPricesBroker
{
    /// <summary>Adds a price row; one with the same provider, model and start replaces it.</summary>
    /// <param name="price">The price.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<ModelPriceModel>> PutAsync(ModelPriceModel price, CancellationToken cancellationToken = default);

    /// <summary>Lists every price row, newest start first per model.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<ModelPriceModel>>> ListAsync(CancellationToken cancellationToken = default);
}
