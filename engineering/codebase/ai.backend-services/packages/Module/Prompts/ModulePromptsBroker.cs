using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;

/// <summary>Integrates the prompt registry in process: use cases, prompt rows in both modes on one table, and the labels
/// of versioned use cases with their move history.</summary>
/// <param name="db">The module's store.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModulePromptsBroker(AiDbContext db, TimeProvider time) : IPromptsBroker
{
    /// <summary>Holds the efforts a prompt may name.</summary>
    private static readonly string[] Efforts = ["none", "low", "medium", "high"];

    /// <summary>Holds how often a versioned save retries when another save took its version first.</summary>
    private const int VersionRetries = 3;

    /// <inheritdoc />
    public async Task<Result<UseCaseModel>> PutUseCaseAsync(UseCaseModel useCase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        if (!AiKeys.IsValid(useCase.Key))
            return Result<UseCaseModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", useCase.Key)));
        if (!Enum.IsDefined(useCase.PromptMode))
            return Result<UseCaseModel>.Fail(AiErrors.Invalid($"'{useCase.PromptMode}' is not a prompt mode."));

        var route = new List<string>();
        foreach (var entry in useCase.Route)
        {
            if (!AiModelRef.TryParse(entry, out var reference) || reference!.ToString().Length > AiConstants.MaxModelRefLength)
                return Result<UseCaseModel>.Fail(AiErrors.Invalid($"The route entry '{entry}' is not a model reference, {{provider}}/{{model}}."));
            route.Add(reference.ToString());
        }

        string? schema = null;
        if (!string.IsNullOrWhiteSpace(useCase.OutputSchema))
        {
            schema = AiJson.NormalizeObject(useCase.OutputSchema);
            if (schema is null)
                return Result<UseCaseModel>.Fail(AiErrors.Invalid("The output schema must be a JSON object."));
        }

        var row = await db.UseCases.FirstOrDefaultAsync(entity => entity.Key == useCase.Key, cancellationToken);
        if (row is null)
        {
            row = new UseCaseEntity { Key = useCase.Key };
            db.UseCases.Add(row);
        }

        row.Description = string.IsNullOrWhiteSpace(useCase.Description) ? null : useCase.Description.Trim();
        row.PromptMode = AiLabels.Mode(useCase.PromptMode);
        row.OutputSchema = schema;
        row.Route = [.. route];
        row.KeepOutputs = useCase.KeepOutputs;
        row.UpdatedAt = time.StoredUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        // Read back, so the schema comes as the store keeps it (jsonb normalizes it).
        var stored = await db.UseCases.AsNoTracking().FirstAsync(entity => entity.Key == useCase.Key, cancellationToken);
        return Result<UseCaseModel>.Ok(AiModels.ToModel(stored));
    }

    /// <inheritdoc />
    public async Task<Result<UseCaseModel>> GetUseCaseAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!AiKeys.IsValid(key))
            return Result<UseCaseModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", key)));

        var row = await db.UseCases.AsNoTracking().FirstOrDefaultAsync(entity => entity.Key == key, cancellationToken);
        return row is null ? Result<UseCaseModel>.Fail(AiErrors.UseCaseNotFound(key)) : Result<UseCaseModel>.Ok(AiModels.ToModel(row));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UseCaseModel>>> ListUseCasesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.UseCases.AsNoTracking().OrderBy(entity => entity.Key).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<UseCaseModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<PromptModel>> SavePromptAsync(PromptDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Validate(draft) is { } invalid)
            return Result<PromptModel>.Fail(invalid);

        var useCase = await db.UseCases.AsNoTracking().FirstOrDefaultAsync(entity => entity.Key == draft.UseCase, cancellationToken);
        if (useCase is null)
            return Result<PromptModel>.Fail(AiErrors.UseCaseNotFound(draft.UseCase));

        var match = ModelMatch.Normalize(draft.ModelMatch);
        var parameters = draft.Parameters ?? new PromptParameters();
        var hash = AiHashes.Content(draft.SystemTemplate, draft.UserTemplate, parameters);
        var by = string.IsNullOrWhiteSpace(draft.By) ? null : draft.By.Trim();
        var note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim();

        if (AiLabels.Read<PromptMode>(useCase.PromptMode) == PromptMode.Rewrite)
        {
            var now = time.StoredUtcNow();
            var row = await db.Prompts
                .Where(entity => entity.UseCase == draft.UseCase && entity.ModelMatch == match)
                .OrderByDescending(entity => entity.Version)
                .FirstOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                row = new PromptEntity { Id = Guid.CreateVersion7(), UseCase = draft.UseCase, ModelMatch = match, Version = 1, CreatedBy = by, CreatedAt = now };
                db.Prompts.Add(row);
            }

            row.SystemTemplate = draft.SystemTemplate;
            row.UserTemplate = draft.UserTemplate;
            row.Parameters = AiJson.WriteParameters(parameters);
            row.ContentHash = hash;
            row.Note = note ?? row.Note;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return Result<PromptModel>.Ok(AiModels.ToModel(row));
        }

        for (var attempt = 1; ; attempt++)
        {
            var now = time.StoredUtcNow();
            var newest = await db.Prompts.Where(entity => entity.UseCase == draft.UseCase).MaxAsync(entity => (int?)entity.Version, cancellationToken) ?? 0;
            var row = new PromptEntity
            {
                Id = Guid.CreateVersion7(),
                UseCase = draft.UseCase,
                ModelMatch = match,
                Version = newest + 1,
                SystemTemplate = draft.SystemTemplate,
                UserTemplate = draft.UserTemplate,
                Parameters = AiJson.WriteParameters(parameters),
                ContentHash = hash,
                Note = note,
                CreatedBy = by,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Prompts.Add(row);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Result<PromptModel>.Ok(AiModels.ToModel(row));
            }
            catch (DbUpdateException exception) when (AiStore.IsUniqueViolation(exception) && attempt < VersionRetries)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PromptModel>>> ListPromptsAsync(string useCase, CancellationToken cancellationToken = default)
    {
        if (await FindUseCaseAsync(useCase, cancellationToken) is { } missing)
            return Result<IReadOnlyList<PromptModel>>.Fail(missing);

        var rows = await db.Prompts.AsNoTracking()
            .Where(entity => entity.UseCase == useCase)
            .OrderByDescending(entity => entity.Version)
            .ThenBy(entity => entity.ModelMatch)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PromptModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<PromptLabelModel>> MoveLabelAsync(string useCase, string label, int version, string? by = null, CancellationToken cancellationToken = default)
    {
        if (!AiKeys.IsValid(label))
            return Result<PromptLabelModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("label", label)));
        if (!AiKeys.IsValid(useCase))
            return Result<PromptLabelModel>.Fail(AiErrors.Invalid(AiKeys.Rejection("use case", useCase)));

        var row = await db.UseCases.AsNoTracking().FirstOrDefaultAsync(entity => entity.Key == useCase, cancellationToken);
        if (row is null)
            return Result<PromptLabelModel>.Fail(AiErrors.UseCaseNotFound(useCase));
        if (AiLabels.Read<PromptMode>(row.PromptMode) != PromptMode.Versioned)
            return Result<PromptLabelModel>.Fail(AiErrors.Invalid($"AI use case '{useCase}' keeps its prompts in rewrite mode, which has no labels."));
        if (!await db.Prompts.AnyAsync(entity => entity.UseCase == useCase && entity.Version == version, cancellationToken))
            return Result<PromptLabelModel>.Fail(AiErrors.Invalid($"AI use case '{useCase}' has no prompt version {version}."));

        var now = time.StoredUtcNow();
        var mover = string.IsNullOrWhiteSpace(by) ? null : by.Trim();
        var current = await db.PromptLabels.FirstOrDefaultAsync(entity => entity.UseCase == useCase && entity.Label == label, cancellationToken);
        var from = current?.Version;
        if (current is null)
        {
            current = new PromptLabelEntity { UseCase = useCase, Label = label };
            db.PromptLabels.Add(current);
        }

        current.Version = version;
        current.MovedAt = now;
        current.MovedBy = mover;
        db.PromptLabelMoves.Add(new PromptLabelMoveEntity
        {
            Id = Guid.CreateVersion7(),
            UseCase = useCase,
            Label = label,
            FromVersion = from,
            Version = version,
            MovedAt = now,
            MovedBy = mover,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result<PromptLabelModel>.Ok(AiModels.ToModel(current));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PromptLabelModel>>> ListLabelsAsync(string useCase, CancellationToken cancellationToken = default)
    {
        if (await FindUseCaseAsync(useCase, cancellationToken) is { } missing)
            return Result<IReadOnlyList<PromptLabelModel>>.Fail(missing);

        var rows = await db.PromptLabels.AsNoTracking().Where(entity => entity.UseCase == useCase).OrderBy(entity => entity.Label).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PromptLabelModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }

    /// <inheritdoc />
    public async Task<Result<PromptModel>> ResolveAsync(string useCase, string model, int? version = null, string? label = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            return Result<PromptModel>.Fail(AiErrors.Invalid("A model id is required to resolve a prompt."));

        var book = await PromptBook.LoadAsync(db, useCase, cancellationToken);
        return book switch
        {
            Result<PromptBook>.Success { Value: var found } => found.Resolve(model.Trim(), version, label),
            Result<PromptBook>.Failure { Error: var error } => Result<PromptModel>.Fail(error),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };
    }

    /// <summary>Validates a draft's key, texts, match and parameters.</summary>
    private static AppError? Validate(PromptDraft draft)
    {
        if (!AiKeys.IsValid(draft.UseCase))
            return AiErrors.Invalid(AiKeys.Rejection("use case", draft.UseCase));
        if (draft.SystemTemplate is null || string.IsNullOrWhiteSpace(draft.UserTemplate))
            return AiErrors.Invalid("A prompt needs a system template and a non-empty user template.");
        if (!ModelMatch.IsValid(ModelMatch.Normalize(draft.ModelMatch)))
            return AiErrors.Invalid($"The model match '{draft.ModelMatch}' must be a model id, or a prefix ending in '*', with no spaces.");

        var parameters = draft.Parameters ?? new PromptParameters();
        if (parameters.Temperature is < 0 or > 2)
            return AiErrors.Invalid("The temperature must be between 0 and 2.");
        if (parameters.MaxOutputTokens is < 1)
            return AiErrors.Invalid("The most output tokens must be 1 or more.");
        if (parameters.Effort is { } effort && !Efforts.Contains(effort, StringComparer.Ordinal))
            return AiErrors.Invalid($"The effort '{effort}' must be one of {string.Join(", ", Efforts)}.");

        return null;
    }

    /// <summary>Checks a use case key and its row; none when it exists.</summary>
    private async Task<AppError?> FindUseCaseAsync(string useCase, CancellationToken cancellationToken)
    {
        if (!AiKeys.IsValid(useCase))
            return AiErrors.Invalid(AiKeys.Rejection("use case", useCase));

        return await db.UseCases.AsNoTracking().AnyAsync(entity => entity.Key == useCase, cancellationToken)
            ? null
            : AiErrors.UseCaseNotFound(useCase);
    }
}
