namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;

/// <summary>Maps a row of <c>ai_use_cases</c>.</summary>
internal sealed class UseCaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PromptMode { get; set; } = string.Empty;
    public string? OutputSchema { get; set; }
    public string[] Route { get; set; } = [];
    public bool KeepOutputs { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Maps a row of <c>ai_prompts</c>: one prompt text of either mode.</summary>
internal sealed class PromptEntity
{
    public Guid Id { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public string? ModelMatch { get; set; }
    public int Version { get; set; }
    public string SystemTemplate { get; set; } = string.Empty;
    public string UserTemplate { get; set; } = string.Empty;
    public string Parameters { get; set; } = "{}";
    public string ContentHash { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Maps a row of <c>ai_prompt_labels</c>: where a label points.</summary>
internal sealed class PromptLabelEntity
{
    public string UseCase { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset MovedAt { get; set; }
    public string? MovedBy { get; set; }
}

/// <summary>Maps a row of <c>ai_prompt_label_moves</c>: one move of a label, kept for the audit.</summary>
internal sealed class PromptLabelMoveEntity
{
    public Guid Id { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int? FromVersion { get; set; }
    public int Version { get; set; }
    public DateTimeOffset MovedAt { get; set; }
    public string? MovedBy { get; set; }
}

/// <summary>Maps a row of <c>ai_model_prices</c>: a model's price from a date on.</summary>
internal sealed class ModelPriceEntity
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public long InputPerMtokMicroUsd { get; set; }
    public long OutputPerMtokMicroUsd { get; set; }
    public long? CacheWritePerMtokMicroUsd { get; set; }
    public long? CacheReadPerMtokMicroUsd { get; set; }
    public string? Source { get; set; }
}

/// <summary>Maps a row of <c>ai_inferences</c>: one model attempt.</summary>
internal sealed class InferenceEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public string PromptHash { get; set; } = string.Empty;
    public string? PromptModelMatch { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public string? Subject { get; set; }
    public string InputHash { get; set; } = string.Empty;
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long CacheWriteTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long CostMicroUsd { get; set; }
    public bool Priced { get; set; }
    public long LatencyMs { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ErrorKind { get; set; }
    public string? Error { get; set; }
    public string? FinishReason { get; set; }
    public string? Output { get; set; }
    public Guid? EvalRunId { get; set; }
}

/// <summary>Maps a row of <c>ai_eval_sets</c>.</summary>
internal sealed class EvalSetEntity
{
    public string Key { get; set; } = string.Empty;
    public string UseCase { get; set; } = string.Empty;
    public string[] ScoreFields { get; set; } = [];
    public string[] SevereMisses { get; set; } = [];
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Maps a row of <c>ai_eval_items</c>; a removed item keeps its row for the runs that scored it.</summary>
internal sealed class EvalItemEntity
{
    public Guid Id { get; set; }
    public string SetKey { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Input { get; set; } = "{}";
    public string Expected { get; set; } = "{}";
    public string[] Tags { get; set; } = [];
    public string? LabelledBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>Maps a row of <c>ai_eval_runs</c>: one cell of the matrix.</summary>
internal sealed class EvalRunEntity
{
    public Guid Id { get; set; }
    public string SetKey { get; set; } = string.Empty;
    public string UseCase { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public string PromptHash { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>Maps a row of <c>ai_eval_scores</c>: one item in one run.</summary>
internal sealed class EvalScoreEntity
{
    public Guid RunId { get; set; }
    public Guid ItemId { get; set; }
    public Guid? InferenceId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public bool Correct { get; set; }
    public bool Severe { get; set; }
    public string[] Misses { get; set; } = [];
    public string? Answer { get; set; }
    public long CostMicroUsd { get; set; }
    public long LatencyMs { get; set; }
    public bool FromCache { get; set; }
    public short? Rating { get; set; }
    public string? Comment { get; set; }
    public string? RatedBy { get; set; }
}

/// <summary>Maps a row of the <c>ai_eval_report_rows</c> view: one item in one run, joined with its run, item and inference.</summary>
internal sealed class EvalReportRow
{
    public string SetKey { get; set; } = string.Empty;
    public Guid RunId { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public string PromptHash { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? RunNote { get; set; }
    public Guid ItemId { get; set; }
    public string? Subject { get; set; }
    public string[] Tags { get; set; } = [];
    public string Expected { get; set; } = "{}";
    public DateTimeOffset ItemCreatedAt { get; set; }
    public Guid? InferenceId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public bool Correct { get; set; }
    public bool Severe { get; set; }
    public string[] Misses { get; set; } = [];
    public string? Answer { get; set; }
    public long CostMicroUsd { get; set; }
    public long LatencyMs { get; set; }
    public bool FromCache { get; set; }
    public short? Rating { get; set; }
    public string? Comment { get; set; }
    public string? RatedBy { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public bool Priced { get; set; }
}
