using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Integrates evals in process: sets and their frozen items, runs that send every item through the runner on one
/// model and one prompt version, scores, ratings, the combined report over <c>ai_eval_report_rows</c>, and export/import.</summary>
/// <remarks>
///   - a run's items go through <see cref="IAiRunner"/> one scope each, so items run in parallel up to the request's
///     degree and every attempt lands in the inference log with the run's id
///   - a removed item keeps its row, so the runs that scored it still report it; new runs, lists and exports skip it
/// </remarks>
/// <param name="db">The module's store.</param>
/// <param name="scopes">Opens one scope per item of a run.</param>
/// <param name="sources">The product's subject sources, which turn a subject into prompt variables once.</param>
/// <param name="options">The module's settings: the parallelism limit.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleEvalsBroker(
    AiDbContext db,
    IServiceScopeFactory scopes,
    IEnumerable<IEvalSubjectSource> sources,
    IOptions<AiModuleOptions> options,
    TimeProvider time) : IEvalsBroker
{
    /// <inheritdoc />
    public async Task<Result<EvalSetModel>> PutSetAsync(EvalSetModel set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (!AiKeys.IsValid(set.Key))
            return Result<EvalSetModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("eval set", set.Key)));
        if (!AiKeys.IsValid(set.UseCase))
            return Result<EvalSetModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", set.UseCase)));
        if (!await db.UseCases.AsNoTracking().AnyAsync(entity => entity.Key == set.UseCase, cancellationToken))
            return Result<EvalSetModel>.Fail(AiErrors.UseCaseNotFound(set.UseCase));

        var row = await db.EvalSets.FirstOrDefaultAsync(entity => entity.Key == set.Key, cancellationToken);
        if (row is null)
        {
            row = new EvalSetEntity { Key = set.Key, CreatedAt = time.StoredUtcNow() };
            db.EvalSets.Add(row);
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
        var found = await FindSetAsync(request.Set, cancellationToken);
        if (found is Result<EvalSetEntity>.Failure { Error: var setError })
            return Result<EvalRunModel>.Fail(setError);
        if (!AiModelRef.TryParse(request.Model, out var reference))
            return Result<EvalRunModel>.Fail(AiErrors.Invalid($"The model reference '{request.Model}' is not {{provider}}/{{model}}."));

        var limit = options.Value.MaxEvalParallelism;
        if (request.Parallelism < 1 || request.Parallelism > limit)
            return Result<EvalRunModel>.Fail(AiErrors.Invalid($"An eval run sends 1 to {limit} items at once, not {request.Parallelism}."));

        var set = ((Result<EvalSetEntity>.Success)found).Value;
        var book = await PromptBook.LoadAsync(db, set.UseCase, cancellationToken);
        if (book is Result<PromptBook>.Failure { Error: var bookError })
            return Result<EvalRunModel>.Fail(bookError);

        var resolved = ((Result<PromptBook>.Success)book).Value.Resolve(reference!.Model, request.Version, request.Label);
        if (resolved is Result<PromptModel>.Failure { Error: var promptError })
            return Result<EvalRunModel>.Fail(promptError);

        var prompt = ((Result<PromptModel>.Success)resolved).Value;
        var run = new EvalRunEntity
        {
            Id = Guid.CreateVersion7(),
            SetKey = set.Key,
            UseCase = set.UseCase,
            PromptVersion = prompt.Version,
            PromptHash = prompt.ContentHash,
            Provider = reference.Provider,
            Model = reference.Model,
            StartedAt = time.StoredUtcNow(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        db.EvalRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var items = await ActiveItemsAsync(set.Key, cancellationToken);
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = request.Parallelism, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(items, parallel, async (item, token) =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<IAiRunner>();
            var store = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            var result = await runner.RunAsync(new AiRunRequest
            {
                UseCase = set.UseCase,
                Variables = item.Input,
                Subject = item.Subject,
                Version = prompt.Version,
                Models = [reference.ToString()],
                Fresh = request.Fresh,
                EvalRunId = run.Id,
            }, token);
            store.EvalScores.Add(ScoreRow(run.Id, item, set, result));
            await store.SaveChangesAsync(token);
        });

        run.FinishedAt = time.StoredUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Result<EvalRunModel>.Ok(AiModels.ToModel(run));
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

    /// <summary>Builds an item's score row from its run's result.</summary>
    private static EvalScoreEntity ScoreRow(Guid runId, EvalItemModel item, EvalSetEntity set, Result<AiRunModel> result)
    {
        if (result is not Result<AiRunModel>.Success { Value: var run })
        {
            return new EvalScoreEntity
            {
                RunId = runId,
                ItemId = item.Id,
                Outcome = AiLabels.Outcome(AiOutcome.Failed),
                Comment = result is Result<AiRunModel>.Failure { Error: var error } ? error.Message : null,
            };
        }

        var row = new EvalScoreEntity
        {
            RunId = runId,
            ItemId = item.Id,
            InferenceId = run.Final.Id,
            Outcome = AiLabels.Outcome(run.Outcome),
            CostMicroUsd = run.CostMicroUsd,
            LatencyMs = run.Attempts.Sum(attempt => attempt.LatencyMs),
            FromCache = run.FromCache,
        };
        if (run.Outcome == AiOutcome.Succeeded)
        {
            var score = EvalScorer.Score(item.Expected, run.Output, set.ScoreFields, set.SevereMisses);
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
