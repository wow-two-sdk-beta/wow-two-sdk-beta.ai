using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Inferences;

/// <summary>Integrates the inference log in process: reads, per-model totals and a subject's purge, each within the data
/// scopes it names.</summary>
/// <param name="db">The module's store.</param>
internal sealed class ModuleInferencesBroker(AiDbContext db) : IInferencesBroker
{
    /// <summary>Holds the most rows one read returns.</summary>
    public const int MaxLimit = 1000;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InferenceModel>>> ListAsync(InferenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > MaxLimit)
            return Result<IReadOnlyList<InferenceModel>>.Fail(AiErrors.Invalid($"The limit must be between 1 and {MaxLimit}."));

        var rows = db.Inferences.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.UseCase))
            rows = rows.Where(entity => entity.UseCase == query.UseCase);
        if (!string.IsNullOrWhiteSpace(query.Subject))
            rows = rows.Where(entity => entity.Subject == query.Subject);
        if (query.EvalRunId is { } run)
            rows = rows.Where(entity => entity.EvalRunId == run);
        if (AiScopes.Filter(query.Scopes) is { } scopes)
            rows = rows.Where(entity => scopes.Contains(entity.Scope));
        if (query.FromUtc is { } from)
        {
            var start = from.ToUniversalTime();
            rows = rows.Where(entity => entity.At >= start);
        }

        if (query.ToUtc is { } to)
        {
            var end = to.ToUniversalTime();
            rows = rows.Where(entity => entity.At < end);
        }

        var list = await rows.OrderByDescending(entity => entity.At).ThenByDescending(entity => entity.Id).Take(query.Limit).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<InferenceModel>>.Ok([.. list.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        TotalsAsync(new InferenceTotalsQuery { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(InferenceTotalsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ToUtc <= query.FromUtc)
            return Result<IReadOnlyList<InferenceTotals>>.Fail(AiErrors.Invalid("The period's end must come after its start."));

        var start = query.FromUtc.ToUniversalTime();
        var end = query.ToUtc.ToUniversalTime();
        var succeeded = AiLabels.Outcome(AiOutcome.Succeeded);
        var rows = db.Inferences.AsNoTracking().Where(entity => entity.At >= start && entity.At < end);
        if (AiScopes.Filter(query.Scopes) is { } scopes)
            rows = rows.Where(entity => scopes.Contains(entity.Scope));

        var totals = await rows
            .GroupBy(entity => new { entity.UseCase, entity.Provider, entity.Model })
            .Select(group => new InferenceTotals
            {
                UseCase = group.Key.UseCase,
                Provider = group.Key.Provider,
                Model = group.Key.Model,
                Attempts = group.LongCount(),
                Succeeded = group.LongCount(entity => entity.Outcome == succeeded),
                InputTokens = group.Sum(entity => entity.InputTokens),
                OutputTokens = group.Sum(entity => entity.OutputTokens),
                CostMicroUsd = group.Sum(entity => entity.CostMicroUsd),
            })
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<InferenceTotals>>.Ok(
            [.. totals.OrderBy(total => total.UseCase, StringComparer.Ordinal).ThenBy(total => total.Provider, StringComparer.Ordinal).ThenBy(total => total.Model, StringComparer.Ordinal)]);
    }

    /// <inheritdoc />
    public Task<Result<int>> ForgetSubjectAsync(string subject, CancellationToken cancellationToken = default) =>
        ForgetSubjectAsync(subject, scopes: null, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<int>> ForgetSubjectAsync(string subject, IReadOnlyList<string>? scopes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return Result<int>.Fail(AiErrors.Invalid("A subject is required."));

        var rows = db.Inferences.Where(entity => entity.Subject == subject && entity.Output != null);
        if (AiScopes.Filter(scopes) is { } kept)
            rows = rows.Where(entity => kept.Contains(entity.Scope));

        var cleared = await rows
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.Output, (string?)null), cancellationToken);
        return Result<int>.Ok(cleared);
    }
}
