namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Represents an eval run: which prompt and model answer a set, and where the run stands.</summary>
public sealed record EvalRunModel
{
    /// <summary>Gets the run id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the set key.</summary>
    public required string Set { get; init; }

    /// <summary>Gets the use case key.</summary>
    public required string UseCase { get; init; }

    /// <summary>Gets the prompt version the run uses, pinned when it was queued.</summary>
    public required int PromptVersion { get; init; }

    /// <summary>Gets the pinned prompt's content hash: its templates and parameters.</summary>
    public required string PromptHash { get; init; }

    /// <summary>Gets the provider name.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model id.</summary>
    public required string Model { get; init; }

    /// <summary>Gets when the run started (UTC); while it is queued, when it was queued.</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets when the run ended (UTC): succeeded, failed or canceled.</summary>
    public DateTimeOffset? FinishedAtUtc { get; init; }

    /// <summary>Gets what the run tries.</summary>
    public string? Note { get; init; }

    /// <summary>Gets where the run stands; a run made before the queue existed reads
    /// <see cref="EvalRunStatus.Succeeded"/>.</summary>
    public EvalRunStatus Status { get; init; } = EvalRunStatus.Succeeded;

    /// <summary>Gets how many items the run takes, its sample included.</summary>
    public int ItemsTotal { get; init; }

    /// <summary>Gets how many of them are scored.</summary>
    public int ItemsDone { get; init; }

    /// <summary>Gets why the run failed, when it did.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the batch the run was queued in, shared by every cell of one matrix.</summary>
    public Guid? BatchId { get; init; }

    /// <summary>Gets who asked for the run.</summary>
    public string? RequestedBy { get; init; }

    /// <summary>Gets when the run last showed it was alive (UTC); a running run beats every few seconds.</summary>
    public DateTimeOffset? HeartbeatAtUtc { get; init; }

    /// <summary>Gets how the items were asked for; none when the run takes the whole set. The items it takes were pinned
    /// when it was queued (<see cref="EvalBatchModel.ItemIds"/>).</summary>
    public EvalSampleModel? Sample { get; init; }

    /// <summary>Gets the data scope the run belongs to: its set's; none for the unscoped bucket.</summary>
    public string? Scope { get; init; }

    /// <summary>Gets the prompt row the run pinned when it was queued; none for a run made before pinning. The run sends
    /// that row's text, parameters and output schema as they were then, whatever a later rewrite or label move did.</summary>
    public Guid? PromptId { get; init; }
}
