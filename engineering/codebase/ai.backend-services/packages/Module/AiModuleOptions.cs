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

    /// <summary>Gets or sets the most item calls one eval request may ask for: models × prompt targets × sampled items. A
    /// request over it is refused before anything is queued or called.</summary>
    public int MaxEvalItemCalls { get; set; } = 2000;

    /// <summary>Gets or sets how often a running eval run beats, beside the beat each finished item gives; a worker fails a
    /// run quiet for <see cref="AiEvalWorkerOptions.OrphanAfter"/> as orphaned, so keep this well below it.</summary>
    public TimeSpan EvalHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets the most images one call or eval item carries, 0 to <see cref="AiConstants.MaxImages"/>; 0
    /// refuses images. A call over it is refused before anything is resolved or called.</summary>
    public int MaxImagesPerCall { get; set; } = 4;

    /// <summary>Gets or sets the largest image in bytes, 1 B to 20 MB; a larger one is refused before any call.</summary>
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Gets or sets the image media types a call may send, each one of <see cref="AiConstants.ImageMediaTypes"/>;
    /// none takes all of them.</summary>
    public string[]? ImageMediaTypes { get; set; }

    /// <summary>Gets or sets how long the module's own HTTP(S) fetch waits for one image before it gives up.</summary>
    public TimeSpan ImageFetchTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the hosts the module's own HTTP(S) fetch may read images from, compared case-insensitively;
    /// a leading <c>*.</c> also admits subdomains. Empty, the default, turns the fetch off, so a caller who may run a
    /// prompt cannot make the host request an arbitrary or internal URL; a host's own <c>IAiImageResolver</c> is
    /// unaffected.</summary>
    public string[] ImageFetchHosts { get; set; } = [];
}
