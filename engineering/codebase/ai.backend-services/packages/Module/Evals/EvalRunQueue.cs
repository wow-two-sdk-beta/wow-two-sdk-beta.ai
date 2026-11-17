using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Moves eval runs through the queue in single statements over the module's own data source: claim, progress,
/// heartbeat, finish, cancel and the orphan sweep.</summary>
/// <remarks>
///   - every state change is a conditional <c>UPDATE … RETURNING</c>, so two workers, a cancel and a finishing run never
///     overwrite each other: only a running run finishes, only a queued or running one cancels
///   - a claim takes the oldest queued run with <c>FOR UPDATE SKIP LOCKED</c>, so workers on several instances never
///     take the same run
///   - timestamps come from the module's clock, at the store's microsecond precision
/// </remarks>
/// <param name="dataSource">The module's keyed Npgsql data source.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class EvalRunQueue([FromKeyedServices(AiModuleConstants.ServiceKey)] NpgsqlDataSource dataSource, TimeProvider time)
{
    private const string Runs = AiModuleConstants.SchemaName + ".ai_eval_runs";

    private static readonly string Queued = AiLabels.Status(EvalRunStatus.Queued);
    private static readonly string Running = AiLabels.Status(EvalRunStatus.Running);

    /// <summary>Claims the oldest queued run: it turns running, starts now and beats now.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run id, or none when nothing is queued.</returns>
    public async Task<Guid?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET status = @running, started_at = @now, heartbeat_at = @now
             WHERE id = (SELECT id FROM {Runs} WHERE status = @queued ORDER BY started_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
             RETURNING id
             """);
        command.Parameters.AddWithValue("running", Running);
        command.Parameters.AddWithValue("queued", Queued);
        command.Parameters.AddWithValue("now", time.StoredUtcNow());
        return await command.ExecuteScalarAsync(cancellationToken) is Guid id ? id : null;
    }

    /// <summary>Counts one more item done and beats.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run's status after the count; none when the run is gone.</returns>
    public async Task<EvalRunStatus?> ProgressAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET items_done = items_done + 1,
                 heartbeat_at = CASE WHEN status = @running THEN @now ELSE heartbeat_at END
             WHERE id = @id
             RETURNING status
             """);
        return await StatusAsync(command, runId, cancellationToken);
    }

    /// <summary>Beats a running run.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run's status; none when the run is gone.</returns>
    public async Task<EvalRunStatus?> BeatAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET heartbeat_at = CASE WHEN status = @running THEN @now ELSE heartbeat_at END
             WHERE id = @id
             RETURNING status
             """);
        return await StatusAsync(command, runId, cancellationToken);
    }

    /// <summary>Ends a running run as <paramref name="status"/>; a run that is no longer running stays as it is.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="status">How it ended.</param>
    /// <param name="error">Why it failed, when it did.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>Whether the run was running and now ended.</returns>
    public async Task<bool> FinishAsync(Guid runId, EvalRunStatus status, string? error, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET status = @status, finished_at = @now, heartbeat_at = @now, error = @error
             WHERE id = @id AND status = @running
             """);
        command.Parameters.AddWithValue("status", AiLabels.Status(status));
        command.Parameters.AddWithValue("error", NpgsqlDbType.Text, (object?)Trim(error) ?? DBNull.Value);
        command.Parameters.AddWithValue("running", Running);
        command.Parameters.AddWithValue("now", time.StoredUtcNow());
        command.Parameters.AddWithValue("id", runId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// <summary>Cancels a queued or running run.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>Whether the run was queued or running and is now canceled.</returns>
    public async Task<bool> CancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET status = @canceled, finished_at = @now
             WHERE id = @id AND status IN (@queued, @running)
             """);
        command.Parameters.AddWithValue("canceled", AiLabels.Status(EvalRunStatus.Canceled));
        command.Parameters.AddWithValue("queued", Queued);
        command.Parameters.AddWithValue("running", Running);
        command.Parameters.AddWithValue("now", time.StoredUtcNow());
        command.Parameters.AddWithValue("id", runId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// <summary>Fails every running run whose heartbeat is older than <paramref name="after"/>: the process running it
    /// stopped without ending it.</summary>
    /// <param name="after">How long a run may go without a heartbeat.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The runs failed.</returns>
    public async Task<IReadOnlyList<Guid>> FailOrphansAsync(TimeSpan after, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        await using var command = dataSource.CreateCommand(
            $"""
             UPDATE {Runs} SET status = @failed, finished_at = @now, error = @error
             WHERE status = @running AND (heartbeat_at IS NULL OR heartbeat_at < @cutoff)
             RETURNING id
             """);
        command.Parameters.AddWithValue("failed", AiLabels.Status(EvalRunStatus.Failed));
        command.Parameters.AddWithValue("running", Running);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("cutoff", now - after);
        command.Parameters.AddWithValue("error", OrphanedError(after));

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    /// <summary>Describes an orphaned run.</summary>
    /// <param name="after">How long a run may go without a heartbeat.</param>
    internal static string OrphanedError(TimeSpan after) =>
        $"Orphaned: the run sent no heartbeat for over {after.TotalSeconds:0} seconds, so the process running it stopped; its scored items stay.";

    /// <summary>Trims an error to the length a run keeps.</summary>
    /// <param name="error">The error.</param>
    internal static string? Trim(string? error) =>
        string.IsNullOrWhiteSpace(error) ? null : error.Length <= 2000 ? error.Trim() : error[..1999].Trim() + "…";

    private async Task<EvalRunStatus?> StatusAsync(NpgsqlCommand command, Guid runId, CancellationToken cancellationToken)
    {
        command.Parameters.AddWithValue("running", Running);
        command.Parameters.AddWithValue("now", time.StoredUtcNow());
        command.Parameters.AddWithValue("id", runId);
        return await command.ExecuteScalarAsync(cancellationToken) is string label ? AiLabels.Read<EvalRunStatus>(label) : null;
    }
}
