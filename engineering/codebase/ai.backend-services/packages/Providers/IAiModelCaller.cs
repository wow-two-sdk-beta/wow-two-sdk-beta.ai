namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Calls a model once, through the provider its reference names; never throws for a provider failure.</summary>
public interface IAiModelCaller
{
    /// <summary>Gets whether the provider <paramref name="provider"/> is configured with a key (or needs none).</summary>
    /// <param name="provider">The provider name.</param>
    bool IsConfigured(string provider);

    /// <summary>Calls <paramref name="request"/>'s model once.</summary>
    /// <param name="request">The attempt.</param>
    /// <param name="cancellationToken">The token that cancels the call; a cancellation by it throws.</param>
    /// <returns>The result: text, or a classified error, with usage and latency. An unconfigured provider answers
    /// <see cref="Runs.AiErrorKind.NotConfigured"/> without a call.</returns>
    Task<AiCallResult> CallAsync(AiCallRequest request, CancellationToken cancellationToken = default);
}
