namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Refers to why one model attempt failed.</summary>
public enum AiErrorKind
{
    /// <summary>The failure matched no known kind.</summary>
    Unknown,

    /// <summary>The provider could not be reached.</summary>
    Network,

    /// <summary>The call exceeded its time budget.</summary>
    Timeout,

    /// <summary>The provider throttled the call (HTTP 429).</summary>
    RateLimited,

    /// <summary>The provider rejected the credentials (HTTP 401 or 403).</summary>
    Unauthorized,

    /// <summary>The provider is overloaded or down (HTTP 5xx, Anthropic 529).</summary>
    Unavailable,

    /// <summary>The request itself was invalid (another HTTP 4xx).</summary>
    BadRequest,

    /// <summary>The model refused, or the provider filtered the content.</summary>
    ContentFiltered,

    /// <summary>The provider answered with an empty or unreadable body.</summary>
    InvalidResponse,

    /// <summary>The answer did not parse against the output schema.</summary>
    SchemaMismatch,

    /// <summary>The route names a provider that has no configured key.</summary>
    NotConfigured,
}
