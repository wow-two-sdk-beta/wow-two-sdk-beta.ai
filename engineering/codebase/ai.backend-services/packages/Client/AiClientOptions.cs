namespace WoW.Two.Sdk.Beta.Be.Ai.Client;

/// <summary>Holds the settings of the Ai HTTP client.</summary>
public sealed class AiClientOptions
{
    /// <summary>Gets or sets the absolute root URL of the running Ai service.</summary>
    public Uri? BaseAddress { get; set; }
}
