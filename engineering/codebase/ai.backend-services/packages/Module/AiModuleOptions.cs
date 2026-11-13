namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Holds the settings of the in-process Ai module.</summary>
public sealed class AiModuleOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string of the database that holds the module's schema.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets how many times the runner retries one model after a transient failure (network, timeout, rate
    /// limit, provider down) before it falls back to the next; 0 to 10.</summary>
    public int RetriesPerModel { get; set; } = 2;

    /// <summary>Gets or sets the wait before the first retry; each later retry waits twice as long.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the most output tokens a call asks for when its prompt names none.</summary>
    public int DefaultMaxOutputTokens { get; set; } = 1024;

    /// <summary>Gets or sets the most items an eval run sends at once, 1 to 16.</summary>
    public int MaxEvalParallelism { get; set; } = 4;
}
