using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;

namespace Ai.Tests.Unit.Prices;

public sealed class PriceMathTests
{
    private static readonly ModelPriceModel Haiku = new()
    {
        Provider = "anthropic",
        Model = "claude-haiku-5-5",
        EffectiveFromUtc = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
        InputPerMTokMicroUsd = 100_000,
        OutputPerMTokMicroUsd = 500_000,
        CacheWritePerMTokMicroUsd = 125_000,
        CacheReadPerMTokMicroUsd = 10_000,
    };

    [Fact]
    public void Cost_ShouldBillUncachedInputCacheReadsWritesAndOutput_AtTheirPrices()
    {
        // 6000 uncached × 0.1 + 3000 read × 0.01 + 1000 written × 0.125 + 2000 out × 0.5 (micro-USD per token)
        PriceMath.Cost(Haiku, inputTokens: 10_000, outputTokens: 2_000, cacheReadTokens: 3_000, cacheWriteTokens: 1_000)
            .Should().Be(600 + 30 + 125 + 1000);
    }

    [Fact]
    public void Cost_ShouldBillCacheTokensAsInput_WhenTheRowNamesNoCachePrice()
    {
        var plain = Haiku with { CacheReadPerMTokMicroUsd = null, CacheWritePerMTokMicroUsd = null };

        PriceMath.Cost(plain, 10_000, 0, 3_000, 1_000).Should().Be(1_000);
    }

    [Fact]
    public void Cost_ShouldRoundToTheNearestMicroUsd()
    {
        PriceMath.Cost(Haiku, inputTokens: 5, outputTokens: 1, cacheReadTokens: 0, cacheWriteTokens: 0).Should().Be(1, "0.5 + 0.5 micro-USD");
        PriceMath.Cost(Haiku, inputTokens: 4, outputTokens: 0, cacheReadTokens: 0, cacheWriteTokens: 0).Should().Be(0, "0.4 micro-USD");
    }

    [Fact]
    public void InEffect_ShouldTakeTheLatestStart_AtOrBeforeTheAttempt()
    {
        var newer = Haiku with { EffectiveFromUtc = Haiku.EffectiveFromUtc.AddDays(10), InputPerMTokMicroUsd = 1 };

        PriceMath.InEffect([Haiku, newer], Haiku.EffectiveFromUtc.AddDays(1)).Should().Be(Haiku);
        PriceMath.InEffect([Haiku, newer], Haiku.EffectiveFromUtc.AddDays(10)).Should().Be(newer);
        PriceMath.InEffect([Haiku, newer], Haiku.EffectiveFromUtc.AddSeconds(-1)).Should().BeNull();
    }
}
