using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Drains the eval queue: fails orphaned runs, claims the oldest queued runs up to
/// <see cref="AiEvalWorkerOptions.MaxConcurrentRuns"/>, runs them, and repeats; any number of instances over one
/// database.</summary>
/// <remarks>
///   - waits on the wall clock: <see cref="AiEvalWorkerOptions.PollInterval"/>, or less when this process queues a run or
///     a full instance frees a slot
///   - a run ends succeeded, canceled (a cancel stops it between items) or failed with the reason; when the host stops
///     mid-run, the run fails as stopped, keeping the items it scored
///   - a failed pass (the database down, say) is logged and retried on the next poll
/// </remarks>
/// <param name="scopes">Opens one scope per run.</param>
/// <param name="queue">Claims, sweeps and ends runs.</param>
/// <param name="signal">Wakes the worker when this process queues a run.</param>
/// <param name="options">The poll interval and the orphan threshold.</param>
/// <param name="logger">Logs each run's start and end.</param>
internal sealed partial class AiEvalWorker(
    IServiceScopeFactory scopes,
    EvalRunQueue queue,
    EvalQueueSignal signal,
    IOptions<AiEvalWorkerOptions> options,
    ILogger<AiEvalWorker> logger) : BackgroundService
{
    /// <summary>The error a run ends with when the host stops while it runs.</summary>
    internal const string StoppedError = "The worker stopped while the run was running; its scored items stay.";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Leave the host's start before the first pass.
        await Task.Yield();
        var settings = options.Value;
        if (!settings.Enabled)
            return;

        var running = new List<Task>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                running.RemoveAll(task => task.IsCompleted);
                var claimed = false;
                try
                {
                    foreach (var orphan in await queue.FailOrphansAsync(settings.OrphanAfter, stoppingToken))
                        LogOrphaned(orphan, settings.OrphanAfter);

                    while (running.Count < settings.MaxConcurrentRuns && await queue.ClaimAsync(stoppingToken) is { } runId)
                    {
                        claimed = true;
                        running.Add(RunOneAsync(runId, stoppingToken));
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogPassFailed(exception, settings.PollInterval);
                }

                if (running.Count >= settings.MaxConcurrentRuns)
                    await Task.WhenAny([.. running, Task.Delay(Timeout.Infinite, stoppingToken)]);
                else if (!claimed)
                    await signal.WaitAsync(settings.PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the runs in flight end as stopped below.
        }
        finally
        {
            await Task.WhenAll(running);
        }
    }

    /// <summary>Runs one claimed run in a scope of its own and logs its start and end.</summary>
    private async Task RunOneAsync(Guid runId, CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var broker = scope.ServiceProvider.GetRequiredService<ModuleEvalsBroker>();
        if (await broker.GetRunAsync(runId, stoppingToken) is Result<EvalRunModel>.Success { Value: var claimed })
            LogStarted(runId, claimed.Set, $"{claimed.Provider}/{claimed.Model}", claimed.PromptVersion, claimed.BatchId);

        var watch = Stopwatch.StartNew();
        try
        {
            var run = await broker.ExecuteQueuedAsync(runId, stoppingToken);
            LogEnded(runId, run.Status, run.ItemsDone, run.ItemsTotal, (long)watch.Elapsed.TotalMilliseconds, run.Error);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await queue.FinishAsync(runId, EvalRunStatus.Failed, StoppedError, CancellationToken.None);
            LogStopped(runId, (long)watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            await queue.FinishAsync(runId, EvalRunStatus.Failed, exception.Message, CancellationToken.None);
            LogRunFailed(exception, runId, (long)watch.Elapsed.TotalMilliseconds);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Eval run {RunId} started: set {Set}, model {Model}, prompt v{PromptVersion}, batch {BatchId}.")]
    private partial void LogStarted(Guid runId, string set, string model, int promptVersion, Guid? batchId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Eval run {RunId} ended {Status}: {ItemsDone} of {ItemsTotal} items in {ElapsedMs} ms. {Error}")]
    private partial void LogEnded(Guid runId, EvalRunStatus status, int itemsDone, int itemsTotal, long elapsedMs, string? error);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Eval run {RunId} failed after {ElapsedMs} ms.")]
    private partial void LogRunFailed(Exception exception, Guid runId, long elapsedMs);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Eval run {RunId} failed after {ElapsedMs} ms: the worker stopped while it ran.")]
    private partial void LogStopped(Guid runId, long elapsedMs);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Eval run {RunId} failed as orphaned: no heartbeat for {OrphanAfter}.")]
    private partial void LogOrphaned(Guid runId, TimeSpan orphanAfter);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "The eval worker's pass failed; it looks again in {PollInterval}.")]
    private partial void LogPassFailed(Exception exception, TimeSpan pollInterval);
}
