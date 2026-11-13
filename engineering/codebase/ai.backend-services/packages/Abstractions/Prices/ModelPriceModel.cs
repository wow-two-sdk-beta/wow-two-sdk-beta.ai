namespace WoW.Two.Sdk.Beta.Be.Ai.Prices;

/// <summary>Represents a model's price from a date on; a newer row supersedes it and history stays.</summary>
public sealed record ModelPriceModel
{
    /// <summary>Gets the provider name, such as <c>anthropic</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model id.</summary>
    public required string Model { get; init; }

    /// <summary>Gets when the price takes effect (UTC).</summary>
    public required DateTimeOffset EffectiveFromUtc { get; init; }

    /// <summary>Gets micro-USD per 1M uncached input tokens.</summary>
    public required long InputPerMTokMicroUsd { get; init; }

    /// <summary>Gets micro-USD per 1M output tokens.</summary>
    public required long OutputPerMTokMicroUsd { get; init; }

    /// <summary>Gets micro-USD per 1M input tokens written to the prompt cache; none bills them as input.</summary>
    public long? CacheWritePerMTokMicroUsd { get; init; }

    /// <summary>Gets micro-USD per 1M input tokens read from the prompt cache; none bills them as input.</summary>
    public long? CacheReadPerMTokMicroUsd { get; init; }

    /// <summary>Gets where the price was read.</summary>
    public string? Source { get; init; }
}
