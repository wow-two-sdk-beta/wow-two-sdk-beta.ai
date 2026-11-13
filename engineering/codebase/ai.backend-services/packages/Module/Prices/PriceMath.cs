using WoW.Two.Sdk.Beta.Be.Ai.Prices;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prices;

/// <summary>Prices one attempt from the price row in effect when it started.</summary>
/// <remarks>Input tokens include the cached ones: the uncached rest bills at the input price, cache reads and writes at
/// their own prices (the input price when a row names none), and output tokens, reasoning included, at the output price.
/// Prices are micro-USD per 1M tokens; the cost rounds to the nearest micro-USD.</remarks>
internal static class PriceMath
{
    private const decimal TokensPerPrice = 1_000_000m;

    /// <summary>Finds the row in effect at <paramref name="at"/>: the latest start at or before it.</summary>
    /// <param name="prices">The model's rows, any order.</param>
    /// <param name="at">The attempt's start.</param>
    public static ModelPriceModel? InEffect(IEnumerable<ModelPriceModel> prices, DateTimeOffset at) =>
        prices.Where(price => price.EffectiveFromUtc <= at).MaxBy(price => price.EffectiveFromUtc);

    /// <summary>Gets an attempt's cost in micro-USD.</summary>
    /// <param name="price">The row in effect.</param>
    /// <param name="inputTokens">The input tokens, cached ones included.</param>
    /// <param name="outputTokens">The output tokens.</param>
    /// <param name="cacheReadTokens">The input tokens read from the prompt cache.</param>
    /// <param name="cacheWriteTokens">The input tokens written to the prompt cache.</param>
    public static long Cost(ModelPriceModel price, long inputTokens, long outputTokens, long cacheReadTokens, long cacheWriteTokens)
    {
        ArgumentNullException.ThrowIfNull(price);
        var read = Math.Max(0, cacheReadTokens);
        var write = Math.Max(0, cacheWriteTokens);
        var uncached = Math.Max(0, inputTokens - read - write);
        var total = (decimal)uncached * price.InputPerMTokMicroUsd
            + (decimal)read * (price.CacheReadPerMTokMicroUsd ?? price.InputPerMTokMicroUsd)
            + (decimal)write * (price.CacheWritePerMTokMicroUsd ?? price.InputPerMTokMicroUsd)
            + (decimal)Math.Max(0, outputTokens) * price.OutputPerMTokMicroUsd;
        return (long)Math.Round(total / TokensPerPrice, MidpointRounding.AwayFromZero);
    }
}
