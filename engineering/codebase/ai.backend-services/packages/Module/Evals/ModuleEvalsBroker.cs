using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Integrates evals in process: sets and their frozen items, batches that pin a sample and its scoring, runs that
/// send the pinned items through the runner on one model with one pinned prompt (now, or queued for a worker), scores,
/// ratings, the combined report over <c>ai_eval_report_rows</c>, and export/import.</summary>
/// <remarks>
///   - a batch pins, when it is queued, the items (in run order, each with its expected answer and input hash) and the
///     set's scoring rules; each run pins its cell's prompt row with its text, parameters and the output schema, so a
///     queued run repeats exactly whatever a later relabel, rewrite or label move does
///   - a run's items go through the runner one scope each, so items run in parallel up to the run's degree and every
///     attempt lands in the inference log with the run's id and its set's data scope
///   - a run counts each item done as it finishes and beats while it runs, so its status shows progress, a cancel stops it
///     between items, and a run whose process died reads as orphaned (<see cref="EvalRunQueue"/>)
///   - a set keeps the data scope it was created in; its batches and runs take it
///   - a removed item keeps its row, so the runs that scored or pinned it still report and run it; new batches, lists and
///     exports skip it
/// </remarks>
/// <param name="db">The module's store.</param>
/// <param name="scopes">Opens one scope per item of a run.</param>
/// <param name="sources">The product's subject sources, which turn a subject into prompt variables once.</param>
/// <param name="options">The module's settings: the parallelism limit and the heartbeat interval.</param>
/// <param name="time">The clock rows are stamped with.</param>
/// <param name="queue">Moves runs through the queue.</param>
/// <param name="signal">Wakes this process's worker when a run is queued.</param>
internal sealed class ModuleEvalsBroker(
    AiDbContext db,
    IServiceScopeFactory scopes,
    IEnumerable<IEvalSubjectSource> sources,
    IOptions<AiModuleOptions> options,
    TimeProvider time,
    EvalRunQueue queue,
    EvalQueueSignal signal) : IEvalsBroker
{
    /// <summary>The most runs one list returns.</summary>
    public const int MaxRunLimit = 1000;

    /// <inheritdoc />
    public async Task<Result<EvalSetModel>> PutSetAsync(EvalSetModel set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (!AiKeys.IsValid(set.Key))
            return Result<EvalSetModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("eval set", set.Key)));
        if (!AiKeys.IsValid(set.UseCase))
            return Result<EvalSetModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", set.UseCase)));
        if (AiScopes.Check(set.Scope) is { } scopeError)
            return Result<EvalSetModel>.Fail(scopeError);
        if (!await db.UseCases.AsNoTracking().AnyAsync(entity => entity.Key == set.UseCase, cancellationToken))
            return Result<EvalSetModel>.Fail(AiErrors.UseCaseNotFound(set.UseCase));

        var scope = AiScopes.Store(set.Scope);
        var row = await db.EvalSets.FirstOrDefaultAsync(entity => entity.Key == set.Key, cancellationToken);
        if (row is null)
        {
            row = new EvalSetEntity { Key = set.Key, CreatedAt = time.StoredUtcNow(), Scope = scope };
            db.EvalSets.Add(row);
        }
        else if (!string.Equals(row.Scope, scope, StringComparison.Ordinal))
        {
            return Result<EvalSetModel>.Fail(AiErrors.EvalSetScopeConflict(set.Key));
        }

        row.UseCase = set.UseCase;
        row.ScoreFields = Clean(set.ScoreFields);
        row.SevereMisses = Clean(set.SevereMisses);
        row.Note = string.IsNullOrWhiteSpace(set.Note) ? null : set.Note.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Result<EvalSetModel>.Ok(AiModels.ToModel(row));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EvalSetModel>>> ListSetsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.EvalSets.AsNoTracking().OrderBy(entity => entity.Key).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<EvalSetModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<EvalSetModel>> GetSetAsync(string set, CancellationToken cancellationToken = default) =>
        await FindSetAsync(set, cancellationToken) switch
        {
            Result<EvalSetEntity>.Success { Value: var row } => Result<EvalSetModel>.Ok(AiModels.ToModel(row)),
            Result<EvalSetEntity>.Failure { Error: var error } => Result<EvalSetModel>.Fail(error),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };

    /// <inheritdoc />
    public async Task<Result<EvalItemModel>> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var row = await db.EvalItems.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == itemId, cancellationToken);
        return row is null ? Result<EvalItemModel>.Fail(AiErrors.EvalItemNotFound(itemId)) : Result<EvalItemModel>.Ok(AiModels.ToModel(row));
    }

    /// <inheritdoc />
    public async Task<Result<EvalItemModel>> AddItemAsync(EvalItemDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var set = await FindSetAsync(draft.Set, cancellationToken);
        if (set is Result<EvalSetEntity>.Failure { Error: var setError })
            return Result<EvalItemModel>.Fail(setError);

        var subject = string.IsNullOrWhiteSpace(draft.Subject) ? null : draft.Subject.Trim();
        if (subject is { Length: > AiConstants.MaxSubjectLength })
            return Result<EvalItemModel>.Fail(AiErrors.Invalid($"A subject is at most {AiConstants.MaxSubjectLength} characters."));
        if (AiJson.NormalizeObject(draft.Expected) is not { } expected)
            return Result<EvalItemModel>.Fail(AiErrors.Invalid("The expected answer must be a JSON object."));

        var input = draft.Input;
        if (input is null)
        {
            if (subject is null)
                return Result<EvalItemModel>.Fail(AiErrors.Invalid("An eval item needs its input or a subject to resolve it from."));

            var useCase = ((Result<EvalSetEntity>.Success)set).Value.UseCase;
            var source = sources.Where(candidate => subject.StartsWith(candidate.Prefix, StringComparison.Ordinal)).MaxBy(candidate => candidate.Prefix.Length);
            input = source is null ? null : await source.ResolveAsync(useCase, subject, cancellationToken);
            if (input is null)
                return Result<EvalItemModel>.Fail(AiErrors.SubjectNotResolved(subject));
        }

        var row = new EvalItemEntity
        {
            Id = Guid.CreateVersion7(),
            SetKey = draft.Set,
            Subject = subject,
            Input = AiJson.WriteVariables(input),
            Expected = expected,
            Tags = Clean(draft.Tags),
            LabelledBy = string.IsNullOrWhiteSpace(draft.LabelledBy) ? null : draft.LabelledBy.Trim(),
            CreatedAt = time.StoredUtcNow(),
        };
        db.EvalItems.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Result<EvalItemModel>.Ok(await ReadItemAsync(row.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<EvalItemModel>> RelabelItemAsync(Guid itemId, string expected, string? by = null, CancellationToken cancellationToken = default)
    {
        if (AiJson.NormalizeObject(expected) is not { } normalized)
            return Result<EvalItemModel>.Fail(AiErrors.Invalid("The expected answer must be a JSON object."));

        var row = await db.EvalItems.FirstOrDefaultAsync(entity => entity.Id == itemId && entity.RemovedAt == null, cancellationToken);
        if (row is null)
            return Result<EvalItemModel>.Fail(AiErrors.EvalItemNotFound(itemId));

        row.Expected = normalized;
        if (!string.IsNullOrWhiteSpace(by))
            row.LabelledBy = by.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Result<EvalItemModel>.Ok(await ReadItemAsync(itemId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var now = time.StoredUtcNow();
        var removed = await db.EvalItems
            .Where(entity => entity.Id == itemId && entity.RemovedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.RemovedAt, now), cancellationToken);
        return Result<bool>.Ok(removed > 0);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EvalItemModel>>> ListItemsAsync(string set, CancellationToken cancellationToken = default)
    {
        if (await FindSetAsync(set, cancellationToken) is Result<EvalSetEntity>.Failure { Error: var error })
            return Result<IReadOnlyList<EvalItemModel>>.Fail(error);

        return Result<IReadOnlyList<EvalItemModel>>.Ok(await ActiveItemsAsync(set, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<EvalRunModel>> RunAsync(EvalRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var planned = await PlanAsync(request.Set, [request.Model], [(request.Version, request.Label)], request.Parallelism, request.RequestedBy,
            EvalSampler.Normalize(request.Count, request.Seed, request.Tags, request.ItemIds), cancellationToken);
        if (planned is Result<BatchPlan>.Failure { Error: var error })
            return Result<EvalRunModel>.Fail(error);

        var plan = ((Result<BatchPlan>.Success)planned).Value;
        var (_, runs) = await SaveBatchAsync(plan, EvalRunStatus.Running, request.Note, request.Fresh, request.Parallelism, request.RequestedBy, cancellationToken);
        var run = runs[0];

        try
        {
            await ExecuteAsync(run.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await queue.FinishAsync(run.Id, EvalRunStatus.Canceled, "The caller stopped the run.", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await queue.FinishAsync(run.Id, EvalRunStatus.Failed, exception.Message, CancellationToken.None);
            throw;
        }

        return Result<EvalRunModel>.Ok(await ReadRunAsync(run.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<EvalBatchModel>> QueueAsync(EvalMatrixRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The prompt axis: each version, then each label; neither runs the version a plain call resolves.
        var targets = new List<(int? Version, string? Label)>();
        targets.AddRange((request.Versions ?? []).Distinct().Select(version => ((int?)version, (string?)null)));
        targets.AddRange((request.Labels ?? []).Where(label => !string.IsNullOrWhiteSpace(label)).Select(label => label.Trim())
            .Distinct(StringComparer.Ordinal).Select(label => ((int?)null, (string?)label)));
        if (targets.Count == 0)
            targets.Add((null, null));

        var planned = await PlanAsync(request.Set, request.Models ?? [], targets, request.Parallelism, request.RequestedBy,
            EvalSampler.Normalize(request.Count, request.Seed, request.Tags, request.ItemIds), cancellationToken);
        if (planned is Result<BatchPlan>.Failure { Error: var error })
            return Result<EvalBatchModel>.Fail(error);

        var plan = ((Result<BatchPlan>.Success)planned).Value;
        var (batch, runs) = await SaveBatchAsync(plan, EvalRunStatus.Queued, request.Note, request.Fresh, request.Parallelism, request.RequestedBy, cancellationToken);
        signal.Notify();
        return Result<EvalBatchModel>.Ok(AiModels.ToModel(batch, [.. plan.Items.Select(item => item.Id)], runs));
    }

    /// <inheritdoc />
    public async Task<Result<EvalRunModel>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var row = await db.EvalRuns.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == runId, cancellationToken);
        return row is null ? Result<EvalRunModel>.Fail(AiErrors.EvalRunNotFound(runId)) : Result<EvalRunModel>.Ok(AiModels.ToModel(row));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(string set, CancellationToken cancellationToken = default)
    {
        if (await FindSetAsync(set, cancellationToken) is Result<EvalSetEntity>.Failure { Error: var error })
            return Result<IReadOnlyList<EvalRunModel>>.Fail(error);

        var rows = await db.EvalRuns.AsNoTracking().Where(entity => entity.SetKey == set)
            .OrderBy(entity => entity.StartedAt).ThenBy(entity => entity.Id).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<EvalRunModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(EvalRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > MaxRunLimit)
            return Result<IReadOnlyList<EvalRunModel>>.Fail(AiErrors.Invalid($"The limit must be between 1 and {MaxRunLimit}."));

        var rows = db.EvalRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Set))
        {
            if (await FindSetAsync(query.Set, cancellationToken) is Result<EvalSetEntity>.Failure { Error: var error })
                return Result<IReadOnlyList<EvalRunModel>>.Fail(error);
            rows = rows.Where(entity => entity.SetKey == query.Set);
        }

        if (query.BatchId is { } batchId)
            rows = rows.Where(entity => entity.BatchId == batchId);
        if (query.Status is { } status)
        {
            var label = AiLabels.Status(status);
            rows = rows.Where(entity => entity.Status == label);
        }

        if (AiScopes.Filter(query.Scopes) is { } scopes)
            rows = rows.Where(entity => scopes.Contains(entity.Scope));

        var list = await rows.OrderByDescending(entity => entity.StartedAt).ThenByDescending(entity => entity.Id).Take(query.Limit).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<EvalRunModel>>.Ok([.. list.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<EvalRunModel>> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await db.EvalRuns.AsNoTracking().AnyAsync(entity => entity.Id == runId, cancellationToken))
            return Result<EvalRunModel>.Fail(AiErrors.EvalRunNotFound(runId));

        await queue.CancelAsync(runId, cancellationToken);
        return Result<EvalRunModel>.Ok(await ReadRunAsync(runId, cancellationToken));
    }

    /// <summary>Runs a run a worker claimed: exactly the items its batch pinned, with the prompt it pinned, scored against
    /// the pinned expected answers and rules.</summary>
    /// <remarks>A run whose pinned items changed or were purged since it was queued, or that pinned no prompt, ends as
    /// failed with the reason; it never runs on what the tables hold now.</remarks>
    /// <param name="runId">The claimed run, now running.</param>
    /// <param name="cancellationToken">The token that stops the run between items; the caller ends the run.</param>
    /// <returns>The run as it ended.</returns>
    internal async Task<EvalRunModel> ExecuteQueuedAsync(Guid runId, CancellationToken cancellationToken)
    {
        await ExecuteAsync(runId, cancellationToken);
        return await ReadRunAsync(runId, CancellationToken.None);
    }

    /// <summary>Checks a request and resolves everything a batch pins: the set and its scoring rules, each cell's prompt
    /// (model × target, deduplicated by model and row) with the use case's output schema, and the sampled items with their
    /// expected answers.</summary>
    private async Task<Result<BatchPlan>> PlanAsync(string setKey, IReadOnlyList<string> modelTexts, IReadOnlyList<(int? Version, string? Label)> targets,
        int parallelism, string? requestedBy, Result<EvalSampleModel> sampled, CancellationToken cancellationToken)
    {
        var found = await FindSetAsync(setKey, cancellationToken);
        if (found is Result<EvalSetEntity>.Failure { Error: var setError })
            return Result<BatchPlan>.Fail(setError);
        if (CheckParallelism(parallelism) is { } parallelismError)
            return Result<BatchPlan>.Fail(parallelismError);
        if (CheckRequestedBy(requestedBy) is { } requesterError)
            return Result<BatchPlan>.Fail(requesterError);
        if (sampled is Result<EvalSampleModel>.Failure { Error: var sampleError })
            return Result<BatchPlan>.Fail(sampleError);

        var models = new List<AiModelRef>();
        foreach (var text in modelTexts.Where(model => !string.IsNullOrWhiteSpace(model)))
        {
            if (!AiModelRef.TryParse(text, out var reference))
                return Result<BatchPlan>.Fail(InvalidModel(text));
            if (!models.Contains(reference!))
                models.Add(reference!);
        }

        if (models.Count == 0)
            return Result<BatchPlan>.Fail(AiErrors.Invalid("An eval run names at least one model."));

        var set = ((Result<EvalSetEntity>.Success)found).Value;
        var loaded = await PromptBook.LoadAsync(db, set.UseCase, cancellationToken);
        if (loaded is Result<PromptBook>.Failure { Error: var bookError })
            return Result<BatchPlan>.Fail(bookError);

        var book = ((Result<PromptBook>.Success)loaded).Value;
        var cells = new List<(AiModelRef Model, PromptModel Prompt)>();
        foreach (var model in models)
        {
            foreach (var (version, label) in targets)
            {
                var resolved = book.Resolve(model.Model, version, label);
                if (resolved is Result<PromptModel>.Failure { Error: var promptError })
                    return Result<BatchPlan>.Fail(promptError);

                var prompt = ((Result<PromptModel>.Success)resolved).Value;
                if (!cells.Any(cell => cell.Model == model && cell.Prompt.Id == prompt.Id))
                    cells.Add((model, prompt));
            }
        }

        var sample = ((Result<EvalSampleModel>.Success)sampled).Value;
        var selected = EvalSampler.Select(set.Key, await ActiveItemsAsync(set.Key, cancellationToken), sample);
        if (selected is Result<IReadOnlyList<EvalItemModel>>.Failure { Error: var selectError })
            return Result<BatchPlan>.Fail(selectError);

        var items = ((Result<IReadOnlyList<EvalItemModel>>.Success)selected).Value;
        var cap = options.Value.MaxEvalItemCalls;
        if ((long)cells.Count * items.Count > cap)
            return Result<BatchPlan>.Fail(AiErrors.Invalid(
                $"{cells.Count} cell{(cells.Count == 1 ? "" : "s")} × {items.Count} items is {cells.Count * items.Count} item calls, over this module's cap of {cap}; sample fewer items or name fewer models."));

        return Result<BatchPlan>.Ok(new BatchPlan(set, book.UseCase.OutputSchema, cells, items, sample));
    }

    /// <summary>Saves a batch with its pinned items and one run per cell, in matrix order: queued for a worker, or running
    /// for the caller, as <paramref name="status"/> says.</summary>
    private async Task<(EvalBatchEntity Batch, IReadOnlyList<EvalRunEntity> Runs)> SaveBatchAsync(BatchPlan plan, EvalRunStatus status, string? note,
        bool fresh, int parallelism, string? requestedBy, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        var batch = new EvalBatchEntity
        {
            Id = Guid.CreateVersion7(),
            SetKey = plan.Set.Key,
            Scope = plan.Set.Scope,
            ScoreFields = plan.Set.ScoreFields,
            SevereMisses = plan.Set.SevereMisses,
            CreatedAt = now,
        };
        db.EvalBatches.Add(batch);
        db.EvalBatchItems.AddRange(plan.Items.Select((item, position) => new EvalBatchItemEntity
        {
            BatchId = batch.Id,
            ItemId = item.Id,
            Position = position,
            Expected = item.Expected,
            InputHash = InputHash(item.Input),
        }));

        var runs = new List<EvalRunEntity>();
        foreach (var (model, prompt) in plan.Cells)
        {
            // A microsecond apart, so workers claim the cells in matrix order.
            var startedAt = now.AddTicks(runs.Count * 10);
            runs.Add(new EvalRunEntity
            {
                Id = Guid.CreateVersion7(),
                SetKey = plan.Set.Key,
                UseCase = plan.Set.UseCase,
                PromptVersion = prompt.Version,
                PromptHash = prompt.ContentHash,
                Provider = model.Provider,
                Model = model.Model,
                StartedAt = startedAt,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = AiLabels.Status(status),
                ItemsTotal = plan.Items.Count,
                BatchId = batch.Id,
                RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy.Trim(),
                HeartbeatAt = status == EvalRunStatus.Running ? startedAt : null,
                Fresh = fresh,
                Parallelism = parallelism,
                SampleCount = plan.Sample.Count,
                SampleSeed = plan.Sample.Seed,
                SampleTags = [.. plan.Sample.Tags],
                SampleItemIds = [.. plan.Sample.ItemIds],
                Scope = plan.Set.Scope,
                PromptId = prompt.Id,
                PromptModelMatch = prompt.ModelMatch,
                PromptSystem = prompt.SystemTemplate,
                PromptUser = prompt.UserTemplate,
                PromptParameters = AiJson.WriteParameters(prompt.Parameters),
                OutputSchema = plan.OutputSchema,
            });
        }

        db.EvalRuns.AddRange(runs);
        await db.SaveChangesAsync(cancellationToken);
        return (batch, runs);
    }

    /// <summary>Sends a running run's pinned items through the runner with its pinned prompt, each in a scope of its own
    /// and up to its parallelism at once, scoring each against its pinned expected answer and counting it done as it
    /// finishes. A heartbeat beats meanwhile; once the run reads anything but running (a cancel), no further item starts.
    /// It ends the run: succeeded when every item ran, failed when its pins no longer hold.</summary>
    private async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        var loaded = await LoadPinnedAsync(runId, cancellationToken);
        if (loaded is Result<PinnedRun>.Failure { Error: var error })
        {
            await queue.FinishAsync(runId, EvalRunStatus.Failed, error.Message, CancellationToken.None);
            return;
        }

        var pinned = ((Result<PinnedRun>.Success)loaded).Value;
        var run = pinned.Run;
        var reference = new AiModelRef(run.Provider, run.Model).ToString();
        using var stop = new CancellationTokenSource();
        using var beating = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = BeatAsync(runId, stop, beating.Token);
        try
        {
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, run.Parallelism), CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(pinned.Items, parallel, async (item, token) =>
            {
                // A cancel stops the run between items: the ones in flight finish and keep their scores.
                if (stop.IsCancellationRequested)
                    return;

                await using var scope = scopes.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<ModuleAiRunner>();
                var store = scope.ServiceProvider.GetRequiredService<AiDbContext>();
                var result = await runner.RunPinnedAsync(new AiRunRequest
                {
                    UseCase = run.UseCase,
                    Variables = item.Item.Input,
                    Subject = item.Item.Subject,
                    Models = [reference],
                    Fresh = run.Fresh,
                    EvalRunId = runId,
                    Scope = run.Scope,
                }, pinned.Prompt, token);
                store.EvalScores.Add(ScoreRow(runId, item.Item.Id, item.Expected, pinned.ScoreFields, pinned.SevereMisses, result));
                await store.SaveChangesAsync(token);
                if (await queue.ProgressAsync(runId, token) is not EvalRunStatus.Running)
                    await stop.CancelAsync();
            });
        }
        finally
        {
            await beating.CancelAsync();
            await heartbeat;
        }

        if (!stop.IsCancellationRequested)
            await queue.FinishAsync(runId, EvalRunStatus.Succeeded, error: null, CancellationToken.None);
    }

    /// <summary>Reads what a run pinned: its prompt, its batch's rules, and its items in run order with the expected
    /// answers they were queued with, checking each item's input against the hash it was pinned with.</summary>
    private async Task<Result<PinnedRun>> LoadPinnedAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.EvalRuns.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == runId, cancellationToken);
        if (run is null)
            return Result<PinnedRun>.Fail(AiErrors.EvalRunNotFound(runId));
        if (run.BatchId is not { } batchId || run.PromptId is not { } promptId || run.PromptSystem is null || run.PromptUser is null)
            return Result<PinnedRun>.Fail(AiErrors.Invalid("The run pinned no batch or prompt, so it cannot repeat what was asked; queue it again."));

        var batch = await db.EvalBatches.AsNoTracking().FirstAsync(entity => entity.Id == batchId, cancellationToken);
        var pins = await db.EvalBatchItems.AsNoTracking().Where(entity => entity.BatchId == batchId)
            .OrderBy(entity => entity.Position).ToListAsync(cancellationToken);
        var ids = pins.Select(pin => pin.ItemId).ToArray();
        var rows = await db.EvalItems.AsNoTracking().Where(entity => ids.Contains(entity.Id)).ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);

        var items = new List<PinnedItem>(pins.Count);
        var drifted = 0;
        foreach (var pin in pins)
        {
            if (!byId.TryGetValue(pin.ItemId, out var row))
            {
                drifted++;
                continue;
            }

            var item = AiModels.ToModel(row);
            if (!string.Equals(InputHash(item.Input), pin.InputHash, StringComparison.Ordinal))
                drifted++;
            else
                items.Add(new PinnedItem(item, AiJson.Compact(pin.Expected)));
        }

        if (drifted > 0)
            return Result<PinnedRun>.Fail(AiErrors.Invalid(
                $"{drifted} of the {pins.Count} pinned items changed or were purged since the run was queued, so it would not repeat; queue it again."));

        var prompt = new PromptModel
        {
            Id = promptId,
            UseCase = run.UseCase,
            ModelMatch = run.PromptModelMatch,
            Version = run.PromptVersion,
            SystemTemplate = run.PromptSystem,
            UserTemplate = run.PromptUser,
            Parameters = run.PromptParameters is { } parameters ? AiJson.ReadParameters(parameters) : new PromptParameters(),
            ContentHash = run.PromptHash,
        };
        return Result<PinnedRun>.Ok(new PinnedRun(run, new PinnedPrompt(prompt, AiJson.CompactOrNull(run.OutputSchema)), items, batch.ScoreFields, batch.SevereMisses));
    }

    /// <summary>Beats every <see cref="AiModuleOptions.EvalHeartbeatInterval"/> on the wall clock until stopped, and
    /// raises <paramref name="stop"/> once the run reads anything but running; a failed beat waits for the next one.</summary>
    private async Task BeatAsync(Guid runId, CancellationTokenSource stop, CancellationToken cancellationToken)
    {
        var interval = options.Value.EvalHeartbeatInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                if (await queue.BeatAsync(runId, cancellationToken) is not EvalRunStatus.Running)
                    await stop.CancelAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
            {
                // The next beat tries again; a run quiet for long enough reads as orphaned.
            }
        }
    }

    /// <summary>Hashes an item's input as a batch pins it: its variables written canonically, keys sorted.</summary>
    /// <param name="input">The item's variables.</param>
    internal static string InputHash(IReadOnlyDictionary<string, string?> input) => AiHashes.Sha256(AiJson.WriteVariables(input));

    private AppError? CheckParallelism(int parallelism)
    {
        var limit = options.Value.MaxEvalParallelism;
        return parallelism < 1 || parallelism > limit
            ? AiErrors.Invalid($"An eval run sends 1 to {limit} items at once, not {parallelism}.")
            : null;
    }

    private static AppError? CheckRequestedBy(string? requestedBy) =>
        requestedBy is { Length: > AiConstants.MaxRequestedByLength }
            ? AiErrors.Invalid($"Who requested a run is at most {AiConstants.MaxRequestedByLength} characters.")
            : null;

    private static AppError InvalidModel(string? model) => AiErrors.Invalid($"The model reference '{model}' is not {{provider}}/{{model}}.");

    private async Task<EvalRunModel> ReadRunAsync(Guid runId, CancellationToken cancellationToken) =>
        AiModels.ToModel(await db.EvalRuns.AsNoTracking().FirstAsync(entity => entity.Id == runId, cancellationToken));

    /// <summary>Holds what a batch pins before it is saved.</summary>
    /// <param name="Set">The set, with its scope and scoring rules.</param>
    /// <param name="OutputSchema">The use case's output schema now.</param>
    /// <param name="Cells">One model and resolved prompt row per cell, in matrix order.</param>
    /// <param name="Items">The sampled items, in run order.</param>
    /// <param name="Sample">How they were asked for.</param>
    private sealed record BatchPlan(EvalSetEntity Set, string? OutputSchema, IReadOnlyList<(AiModelRef Model, PromptModel Prompt)> Cells,
        IReadOnlyList<EvalItemModel> Items, EvalSampleModel Sample);

    /// <summary>Holds a pinned item with the expected answer it was queued with.</summary>
    private sealed record PinnedItem(EvalItemModel Item, string Expected);

    /// <summary>Holds what a run pinned, read back for its execution.</summary>
    private sealed record PinnedRun(EvalRunEntity Run, PinnedPrompt Prompt, IReadOnlyList<PinnedItem> Items, IReadOnlyList<string> ScoreFields,
        IReadOnlyList<string> SevereMisses);

    /// <inheritdoc />
    public async Task<Result<EvalItemResult>> RateAsync(EvalRatingDraft rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);
        if (rating.Rating is < 1 or > 5)
            return Result<EvalItemResult>.Fail(AiErrors.Invalid($"A rating is 1 to 5, not {rating.Rating}."));

        var score = await db.EvalScores.FirstOrDefaultAsync(entity => entity.RunId == rating.RunId && entity.ItemId == rating.ItemId, cancellationToken);
        if (score is null)
        {
            return await db.EvalRuns.AsNoTracking().AnyAsync(entity => entity.Id == rating.RunId, cancellationToken)
                ? Result<EvalItemResult>.Fail(AiErrors.EvalScoreNotFound(rating.RunId, rating.ItemId))
                : Result<EvalItemResult>.Fail(AiErrors.EvalRunNotFound(rating.RunId));
        }

        score.Rating = (short)rating.Rating;
        score.Comment = string.IsNullOrWhiteSpace(rating.Comment) ? null : rating.Comment.Trim();
        score.RatedBy = string.IsNullOrWhiteSpace(rating.By) ? null : rating.By.Trim();
        await db.SaveChangesAsync(cancellationToken);

        var row = await db.EvalReportRows.AsNoTracking().FirstAsync(entity => entity.RunId == rating.RunId && entity.ItemId == rating.ItemId, cancellationToken);
        return Result<EvalItemResult>.Ok(AiModels.ToResult(row, withAnswer: true));
    }

    /// <inheritdoc />
    public async Task<Result<EvalReportModel>> ReportAsync(string set, IReadOnlyList<Guid>? runIds = null, bool withAnswers = false, CancellationToken cancellationToken = default)
    {
        var found = await FindSetAsync(set, cancellationToken);
        if (found is Result<EvalSetEntity>.Failure { Error: var error })
            return Result<EvalReportModel>.Fail(error);

        var runs = db.EvalRuns.AsNoTracking().Where(entity => entity.SetKey == set);
        if (runIds is { Count: > 0 })
        {
            var ids = runIds.ToArray();
            var known = await runs.Where(entity => ids.Contains(entity.Id)).Select(entity => entity.Id).ToListAsync(cancellationToken);
            if (ids.FirstOrDefault(id => !known.Contains(id)) is var unknown && unknown != Guid.Empty)
                return Result<EvalReportModel>.Fail(AiErrors.EvalRunNotFound(unknown));
            runs = runs.Where(entity => ids.Contains(entity.Id));
        }

        var runRows = await runs.OrderBy(entity => entity.StartedAt).ThenBy(entity => entity.Id).ToListAsync(cancellationToken);
        var runKeys = runRows.Select(entity => entity.Id).ToArray();
        var rows = await db.EvalReportRows.AsNoTracking().Where(entity => runKeys.Contains(entity.RunId)).ToListAsync(cancellationToken);
        var byRun = rows.ToLookup(row => row.RunId);

        var summaries = new List<EvalRunSummary>();
        var results = new List<EvalItemResult>();
        foreach (var runRow in runRows)
        {
            var runItems = byRun[runRow.Id].OrderBy(row => row.ItemCreatedAt).ThenBy(row => row.ItemId).ToList();
            var itemResults = runItems.Select(row => AiModels.ToResult(row, withAnswers)).ToList();
            var tokens = (runItems.Sum(row => row.InputTokens), runItems.Sum(row => row.OutputTokens), runItems.Sum(row => row.CacheReadTokens));
            summaries.Add(EvalReportMath.Summarize(AiModels.ToModel(runRow), itemResults, tokens));
            results.AddRange(itemResults);
        }

        return Result<EvalReportModel>.Ok(new EvalReportModel
        {
            Set = set,
            UseCase = ((Result<EvalSetEntity>.Success)found).Value.UseCase,
            Runs = summaries,
            Items = results,
        });
    }

    /// <inheritdoc />
    public async Task<Result<EvalSetExport>> ExportSetAsync(string set, CancellationToken cancellationToken = default)
    {
        var found = await FindSetAsync(set, cancellationToken);
        if (found is Result<EvalSetEntity>.Failure { Error: var error })
            return Result<EvalSetExport>.Fail(error);

        return Result<EvalSetExport>.Ok(new EvalSetExport
        {
            Set = AiModels.ToModel(((Result<EvalSetEntity>.Success)found).Value),
            Items = await ActiveItemsAsync(set, cancellationToken),
        });
    }

    /// <inheritdoc />
    public async Task<Result<EvalSetModel>> ImportSetAsync(EvalSetExport export, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (!string.Equals(export.Format, EvalSetExport.FormatV1, StringComparison.Ordinal))
            return Result<EvalSetModel>.Fail(AiErrors.Invalid($"The export's format is '{export.Format}', not '{EvalSetExport.FormatV1}'."));
        if (export.Set is null || export.Items is null)
            return Result<EvalSetModel>.Fail(AiErrors.Invalid("The export carries no set."));

        var items = new List<EvalItemEntity>();
        var now = time.StoredUtcNow();
        foreach (var item in export.Items)
        {
            if (AiJson.NormalizeObject(item.Expected) is not { } expected)
                return Result<EvalSetModel>.Fail(AiErrors.Invalid($"Eval item '{item.Id}' expects no JSON object."));
            if (item.Subject is { Length: > AiConstants.MaxSubjectLength })
                return Result<EvalSetModel>.Fail(AiErrors.Invalid($"Eval item '{item.Id}' has a subject over {AiConstants.MaxSubjectLength} characters."));

            items.Add(new EvalItemEntity
            {
                Id = item.Id == Guid.Empty ? Guid.CreateVersion7() : item.Id,
                SetKey = export.Set.Key,
                Subject = item.Subject,
                Input = AiJson.WriteVariables(item.Input ?? new Dictionary<string, string?>()),
                Expected = expected,
                Tags = Clean(item.Tags ?? []),
                LabelledBy = item.LabelledBy,
                CreatedAt = now,
            });
        }

        var put = await PutSetAsync(export.Set, cancellationToken);
        if (put is Result<EvalSetModel>.Failure)
            return put;

        var ids = items.Select(item => item.Id).ToArray();
        var existing = await db.EvalItems.AsNoTracking().Where(entity => ids.Contains(entity.Id)).Select(entity => entity.Id).ToListAsync(cancellationToken);
        var created = now;
        foreach (var item in items.Where(item => !existing.Contains(item.Id)).DistinctBy(item => item.Id))
        {
            // Keep the export's order in created_at, the order runs and reports follow.
            item.CreatedAt = created;
            created = created.AddTicks(10);
            db.EvalItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        return put;
    }

    /// <summary>Builds an item's score row from its run's result, scored against the expected answer and rules the run
    /// pinned; a run that could not start for the item (its use case gone, a variable missing) scores as failed with why.</summary>
    private static EvalScoreEntity ScoreRow(Guid runId, Guid itemId, string expected, IReadOnlyList<string> scoreFields, IReadOnlyList<string> severeMisses,
        Result<AiRunModel> result)
    {
        if (result is not Result<AiRunModel>.Success { Value: var run })
        {
            return new EvalScoreEntity
            {
                RunId = runId,
                ItemId = itemId,
                Outcome = AiLabels.Outcome(AiOutcome.Failed),
                Comment = result is Result<AiRunModel>.Failure { Error: var error } ? error.Message : null,
                Expected = expected,
            };
        }

        var row = new EvalScoreEntity
        {
            RunId = runId,
            ItemId = itemId,
            InferenceId = run.Final.Id,
            Outcome = AiLabels.Outcome(run.Outcome),
            CostMicroUsd = run.CostMicroUsd,
            LatencyMs = run.Attempts.Sum(attempt => attempt.LatencyMs),
            FromCache = run.FromCache,
            Expected = expected,
        };
        if (run.Outcome == AiOutcome.Succeeded)
        {
            var score = EvalScorer.Score(expected, run.Output, scoreFields, severeMisses);
            row.Correct = score.Correct;
            row.Severe = score.Severe;
            row.Misses = [.. score.Misses];
            row.Answer = score.Answer;
        }

        return row;
    }

    /// <summary>Finds a set by key.</summary>
    private async Task<Result<EvalSetEntity>> FindSetAsync(string set, CancellationToken cancellationToken)
    {
        if (!AiKeys.IsValid(set))
            return Result<EvalSetEntity>.Fail(AiErrors.Invalid(AiKeys.Rejection("eval set", set)));

        var row = await db.EvalSets.AsNoTracking().FirstOrDefaultAsync(entity => entity.Key == set, cancellationToken);
        return row is null ? Result<EvalSetEntity>.Fail(AiErrors.EvalSetNotFound(set)) : Result<EvalSetEntity>.Ok(row);
    }

    /// <summary>Lists a set's items that are not removed, oldest first.</summary>
    private async Task<IReadOnlyList<EvalItemModel>> ActiveItemsAsync(string set, CancellationToken cancellationToken)
    {
        var rows = await db.EvalItems.AsNoTracking()
            .Where(entity => entity.SetKey == set && entity.RemovedAt == null)
            .OrderBy(entity => entity.CreatedAt).ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(AiModels.ToModel)];
    }

    private async Task<EvalItemModel> ReadItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        AiModels.ToModel(await db.EvalItems.AsNoTracking().FirstAsync(entity => entity.Id == itemId, cancellationToken));

    /// <summary>Trims a list, dropping blanks and repeats, keeping its order.</summary>
    private static string[] Clean(IEnumerable<string> values) =>
        [.. values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal)];
}
