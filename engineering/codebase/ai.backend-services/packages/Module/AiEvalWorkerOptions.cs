namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Holds the settings of the eval worker (<c>AddAiEvalWorker</c>).</summary>
public sealed class AiEvalWorkerOptions
{
    /// <summary>Gets or sets whether the worker drains the queue; false keeps a host that composes it from claiming any
    /// run, as on an instance that only serves the API. Read when the host starts.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets how long the worker waits for a queued run before it looks again; a run queued in this
    /// process wakes it at once.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets how long a running run may go without a heartbeat before the worker fails it as orphaned; keep
    /// it well above <see cref="AiModuleOptions.EvalHeartbeatInterval"/>.</summary>
    public TimeSpan OrphanAfter { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets how many runs one instance runs at once, 1 (the default) to 16; each run sends up to its own
    /// parallelism of items at once.</summary>
    public int MaxConcurrentRuns { get; set; } = 1;
}
