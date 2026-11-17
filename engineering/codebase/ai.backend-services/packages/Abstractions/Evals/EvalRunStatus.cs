namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Refers to where an eval run stands: queued, running, or ended one of three ways.</summary>
public enum EvalRunStatus
{
    /// <summary>Waiting for a worker; nothing is scored yet.</summary>
    Queued,

    /// <summary>Sending its items; <see cref="EvalRunModel.ItemsDone"/> grows as they finish.</summary>
    Running,

    /// <summary>Every item was sent and scored; a failed or refused model answer still counts as scored.</summary>
    Succeeded,

    /// <summary>Stopped by an error, or orphaned when its worker stopped beating; <see cref="EvalRunModel.Error"/> says why.</summary>
    Failed,

    /// <summary>Canceled before or while it ran; items scored before the cancel stay in the report.</summary>
    Canceled,
}
