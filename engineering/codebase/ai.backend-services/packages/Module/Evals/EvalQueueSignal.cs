namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Wakes this process's eval worker when a run is queued here, so it need not wait for its next poll; runs
/// queued by another process wait for that poll.</summary>
internal sealed class EvalQueueSignal : IDisposable
{
    private readonly SemaphoreSlim gate = new(0, 1);

    /// <summary>Wakes the worker; repeated calls before it wakes count once.</summary>
    public void Notify()
    {
        try
        {
            gate.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled.
        }
    }

    /// <summary>Waits for a signal or the timeout, on the wall clock.</summary>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="cancellationToken">The token that ends the wait.</param>
    /// <returns>Whether a signal woke it.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => gate.WaitAsync(timeout, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => gate.Dispose();
}
