namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Refers to how a run, or one attempt of it, ended.</summary>
public enum AiOutcome
{
    /// <summary>The model answered and the answer matched the use case's output schema.</summary>
    Succeeded,

    /// <summary>Every attempt failed: network, timeout, rate limit, credentials, provider down, or not configured.</summary>
    Failed,

    /// <summary>The model refused or the provider filtered the content.</summary>
    Refused,

    /// <summary>The model answered, but the answer was not valid JSON for the output schema.</summary>
    SchemaInvalid,
}
