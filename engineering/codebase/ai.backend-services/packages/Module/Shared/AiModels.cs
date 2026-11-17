using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Maps the store's rows to the contract's models.</summary>
internal static class AiModels
{
    /// <summary>Maps a use case row.</summary>
    public static UseCaseModel ToModel(UseCaseEntity row) => new()
    {
        Key = row.Key,
        Description = row.Description,
        PromptMode = AiLabels.Read<PromptMode>(row.PromptMode),
        OutputSchema = AiJson.CompactOrNull(row.OutputSchema),
        Route = row.Route,
        KeepOutputs = row.KeepOutputs,
        UpdatedAtUtc = row.UpdatedAt,
    };

    /// <summary>Maps a prompt row.</summary>
    public static PromptModel ToModel(PromptEntity row) => new()
    {
        Id = row.Id,
        UseCase = row.UseCase,
        ModelMatch = row.ModelMatch,
        Version = row.Version,
        SystemTemplate = row.SystemTemplate,
        UserTemplate = row.UserTemplate,
        Parameters = AiJson.ReadParameters(row.Parameters),
        ContentHash = row.ContentHash,
        Note = row.Note,
        CreatedBy = row.CreatedBy,
        CreatedAtUtc = row.CreatedAt,
        UpdatedAtUtc = row.UpdatedAt,
    };

    /// <summary>Maps a label row.</summary>
    public static PromptLabelModel ToModel(PromptLabelEntity row) => new()
    {
        UseCase = row.UseCase,
        Label = row.Label,
        Version = row.Version,
        MovedAtUtc = row.MovedAt,
        MovedBy = row.MovedBy,
    };

    /// <summary>Maps a price row.</summary>
    public static ModelPriceModel ToModel(ModelPriceEntity row) => new()
    {
        Provider = row.Provider,
        Model = row.Model,
        EffectiveFromUtc = row.EffectiveFrom,
        InputPerMTokMicroUsd = row.InputPerMtokMicroUsd,
        OutputPerMTokMicroUsd = row.OutputPerMtokMicroUsd,
        CacheWritePerMTokMicroUsd = row.CacheWritePerMtokMicroUsd,
        CacheReadPerMTokMicroUsd = row.CacheReadPerMtokMicroUsd,
        Source = row.Source,
    };

    /// <summary>Maps an inference row.</summary>
    public static InferenceModel ToModel(InferenceEntity row) => new()
    {
        Id = row.Id,
        AtUtc = row.At,
        UseCase = row.UseCase,
        PromptVersion = row.PromptVersion,
        PromptHash = row.PromptHash,
        PromptModelMatch = row.PromptModelMatch,
        Provider = row.Provider,
        Model = row.Model,
        Attempt = row.Attempt,
        Subject = row.Subject,
        InputHash = row.InputHash,
        ImageCount = row.ImageCount,
        ImageHashes = row.ImageHashes,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CacheReadTokens = row.CacheReadTokens,
        CacheWriteTokens = row.CacheWriteTokens,
        ReasoningTokens = row.ReasoningTokens,
        CostMicroUsd = row.CostMicroUsd,
        Priced = row.Priced,
        LatencyMs = row.LatencyMs,
        Outcome = AiLabels.Read<AiOutcome>(row.Outcome),
        ErrorKind = row.ErrorKind is { } kind ? AiLabels.Read<AiErrorKind>(kind) : null,
        Error = row.Error,
        FinishReason = row.FinishReason,
        Output = row.Output,
        EvalRunId = row.EvalRunId,
        Scope = AiScopes.Read(row.Scope),
    };

    /// <summary>Maps an eval set row.</summary>
    public static EvalSetModel ToModel(EvalSetEntity row) => new()
    {
        Key = row.Key,
        UseCase = row.UseCase,
        ScoreFields = row.ScoreFields,
        SevereMisses = row.SevereMisses,
        Note = row.Note,
        CreatedAtUtc = row.CreatedAt,
        Scope = AiScopes.Read(row.Scope),
    };

    /// <summary>Maps an eval item row.</summary>
    public static EvalItemModel ToModel(EvalItemEntity row) => new()
    {
        Id = row.Id,
        Set = row.SetKey,
        Subject = row.Subject,
        Input = AiJson.ReadVariables(row.Input),
        Images = AiJson.ReadImages(row.Images),
        Expected = AiJson.Compact(row.Expected),
        Tags = row.Tags,
        LabelledBy = row.LabelledBy,
    };

    /// <summary>Maps an eval run row.</summary>
    public static EvalRunModel ToModel(EvalRunEntity row) => new()
    {
        Id = row.Id,
        Set = row.SetKey,
        UseCase = row.UseCase,
        PromptVersion = row.PromptVersion,
        PromptHash = row.PromptHash,
        Provider = row.Provider,
        Model = row.Model,
        StartedAtUtc = row.StartedAt,
        FinishedAtUtc = row.FinishedAt,
        Note = row.Note,
        Status = AiLabels.Read<EvalRunStatus>(row.Status),
        ItemsTotal = row.ItemsTotal,
        ItemsDone = row.ItemsDone,
        Error = row.Error,
        BatchId = row.BatchId,
        RequestedBy = row.RequestedBy,
        HeartbeatAtUtc = row.HeartbeatAt,
        Sample = row.SampleCount is null && row.SampleSeed is null && row.SampleTags.Length == 0 && row.SampleItemIds.Length == 0
            ? null
            : new EvalSampleModel { Count = row.SampleCount, Seed = row.SampleSeed, Tags = row.SampleTags, ItemIds = row.SampleItemIds },
        Scope = AiScopes.Read(row.Scope),
        PromptId = row.PromptId,
    };

    /// <summary>Maps a batch row with its pinned items and its runs.</summary>
    /// <param name="row">The batch row.</param>
    /// <param name="itemIds">Its items, in run order.</param>
    /// <param name="runs">Its runs, in matrix order.</param>
    public static EvalBatchModel ToModel(EvalBatchEntity row, IReadOnlyList<Guid> itemIds, IReadOnlyList<EvalRunEntity> runs) => new()
    {
        Id = row.Id,
        Set = row.SetKey,
        Scope = AiScopes.Read(row.Scope),
        ItemIds = itemIds,
        ScoreFields = row.ScoreFields,
        SevereMisses = row.SevereMisses,
        Sample = runs.Count == 0 ? null : ToModel(runs[0]).Sample,
        RequestedBy = runs.Count == 0 ? null : runs[0].RequestedBy,
        CreatedAtUtc = row.CreatedAt,
        Runs = [.. runs.Select(ToModel)],
    };

    /// <summary>Maps a report row to an item result.</summary>
    /// <param name="row">The row.</param>
    /// <param name="withAnswer">Whether to carry the scored answer fields.</param>
    public static EvalItemResult ToResult(EvalReportRow row, bool withAnswer) => new()
    {
        RunId = row.RunId,
        ItemId = row.ItemId,
        Subject = row.Subject,
        Tags = row.Tags,
        Expected = AiJson.Compact(row.Expected),
        Answer = withAnswer ? AiJson.CompactOrNull(row.Answer) : null,
        Correct = row.Correct,
        Severe = row.Severe,
        Misses = row.Misses,
        InferenceId = row.InferenceId,
        CostMicroUsd = row.CostMicroUsd,
        LatencyMs = row.LatencyMs,
        Outcome = AiLabels.Read<AiOutcome>(row.Outcome),
        FromCache = row.FromCache,
        Rating = row.Rating,
        Comment = row.Comment,
        PromptHash = row.PromptHash,
        Priced = row.Priced,
    };
}
