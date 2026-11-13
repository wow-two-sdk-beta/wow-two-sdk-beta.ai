using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Tells which failed attempts are worth another try.</summary>
public static class AiRetry
{
    /// <summary>Gets whether an attempt that failed with <paramref name="kind"/> may succeed when tried again.</summary>
    /// <param name="kind">The attempt's error kind.</param>
    /// <returns>True for <see cref="AiErrorKind.Network"/>, <see cref="AiErrorKind.Timeout"/>,
    /// <see cref="AiErrorKind.RateLimited"/> and <see cref="AiErrorKind.Unavailable"/>.</returns>
    public static bool IsTransient(AiErrorKind kind) =>
        kind is AiErrorKind.Network or AiErrorKind.Timeout or AiErrorKind.RateLimited or AiErrorKind.Unavailable;
}
