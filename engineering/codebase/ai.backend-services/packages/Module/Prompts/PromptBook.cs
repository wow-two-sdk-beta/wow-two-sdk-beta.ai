using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;

/// <summary>Holds one use case with every prompt row and label, read once, so a run resolves each routed model without
/// another query.</summary>
/// <param name="UseCase">The use case.</param>
/// <param name="Rows">Its prompt rows.</param>
/// <param name="Labels">Its labels and the versions they point at.</param>
internal sealed record PromptBook(UseCaseModel UseCase, IReadOnlyList<PromptModel> Rows, IReadOnlyDictionary<string, int> Labels)
{
    /// <summary>Reads a use case's book.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="useCase">The use case key.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The book, or the failure for an invalid key or a missing use case.</returns>
    public static async Task<Result<PromptBook>> LoadAsync(AiDbContext db, string useCase, CancellationToken cancellationToken)
    {
        if (!AiKeys.IsValid(useCase))
            return Result<PromptBook>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", useCase)));

        var row = await db.UseCases.AsNoTracking().FirstOrDefaultAsync(entity => entity.Key == useCase, cancellationToken);
        if (row is null)
            return Result<PromptBook>.Fail(AiErrors.UseCaseNotFound(useCase));

        var prompts = await db.Prompts.AsNoTracking().Where(entity => entity.UseCase == useCase).ToListAsync(cancellationToken);
        var labels = await db.PromptLabels.AsNoTracking().Where(entity => entity.UseCase == useCase).ToListAsync(cancellationToken);
        return Result<PromptBook>.Ok(new PromptBook(
            AiModels.ToModel(row),
            [.. prompts.Select(AiModels.ToModel)],
            labels.ToDictionary(label => label.Label, label => label.Version, StringComparer.Ordinal)));
    }

    /// <summary>Finds the version a call targets: <paramref name="version"/>, else <paramref name="label"/>'s, else the
    /// production label's, else the newest; 0 when the use case has no prompt.</summary>
    /// <param name="version">A version to target.</param>
    /// <param name="label">A label to target.</param>
    /// <returns>The target, or the failure for a version below 1 or a label the use case does not have.</returns>
    public Result<int> Target(int? version, string? label)
    {
        if (version is { } explicitVersion)
            return explicitVersion > 0
                ? Result<int>.Ok(explicitVersion)
                : Result<int>.Fail(AiErrors.Invalid($"A prompt version must be 1 or more, not {explicitVersion}."));

        if (!string.IsNullOrWhiteSpace(label))
            return Labels.TryGetValue(label.Trim(), out var labelled)
                ? Result<int>.Ok(labelled)
                : Result<int>.Fail(AiErrors.LabelNotFound(UseCase.Key, label.Trim()));

        if (Labels.TryGetValue(AiConstants.ProductionLabel, out var production))
            return Result<int>.Ok(production);

        return Result<int>.Ok(Rows.Count == 0 ? 0 : Rows.Max(row => row.Version));
    }

    /// <summary>Resolves the row that serves <paramref name="model"/> for a call.</summary>
    /// <param name="model">The routed model id.</param>
    /// <param name="version">A version to target.</param>
    /// <param name="label">A label to target.</param>
    /// <returns>The row; <see cref="AiErrors.PromptNotFound"/> when none fits.</returns>
    public Result<PromptModel> Resolve(string model, int? version, string? label)
    {
        var target = Target(version, label);
        if (target is Result<int>.Failure { Error: var error })
            return Result<PromptModel>.Fail(error);

        return ModelMatch.Pick(Rows, model, ((Result<int>.Success)target).Value) is { } row
            ? Result<PromptModel>.Ok(row)
            : Result<PromptModel>.Fail(AiErrors.PromptNotFound(UseCase.Key, model));
    }
}
