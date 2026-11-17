using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Keeps eval sets and runs them: each run sends every item through one prompt version on one model, scores
/// the answers, and records them in the inference log, so one report joins accuracy, tokens, cost and ratings.</summary>
public interface IEvalsBroker
{
    /// <summary>Creates or replaces a set's definition; its items stay. A set keeps the data scope it was created in: a put
    /// naming another scope fails as a conflict.</summary>
    /// <param name="set">The set.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalSetModel>> PutSetAsync(EvalSetModel set, CancellationToken cancellationToken = default);

    /// <summary>Lists every set.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalSetModel>>> ListSetsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads a set.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalSetModel>> GetSetAsync(string set, CancellationToken cancellationToken = default);

    /// <summary>Adds an item, freezing its input.</summary>
    /// <param name="draft">The item.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalItemModel>> AddItemAsync(EvalItemDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Changes an item's expected answer, keeping its input.</summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="expected">The expected answer as a JSON object.</param>
    /// <param name="by">Who changed it.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalItemModel>> RelabelItemAsync(Guid itemId, string expected, string? by = null, CancellationToken cancellationToken = default);

    /// <summary>Removes an item; its past scores stay in the runs that made them.</summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Reads an item, removed ones included.</summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalItemModel>> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Lists a set's items.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalItemModel>>> ListItemsAsync(string set, CancellationToken cancellationToken = default);

    /// <summary>Runs a set through one prompt version on one model, now, and scores every item it samples: a batch of one
    /// cell, pinned like a queued one and run by the caller.</summary>
    /// <remarks>The run shows as <see cref="EvalRunStatus.Running"/> with its progress and heartbeat while it runs, and
    /// stops between items when <see cref="CancelRunAsync"/> cancels it.</remarks>
    /// <param name="request">The run.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalRunModel>> RunAsync(EvalRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>Queues a matrix: one <see cref="EvalRunStatus.Queued"/> run per model × version or label, sharing one
    /// batch, for a worker to run. The sample, the expected answers and the scoring rules are pinned on the batch, and
    /// each cell's resolved prompt (row, version, text, parameters and output schema) on its run, so a later relabel,
    /// rewrite or label move never changes what a queued run sends or how it scores; nothing is queued when one cell
    /// cannot start, or when models × items exceed the module's cap.</summary>
    /// <param name="request">The matrix.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The batch with its queued runs, in matrix order.</returns>
    Task<Result<EvalBatchModel>> QueueAsync(EvalMatrixRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads a run with its status and progress.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalRunModel>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Lists a set's runs, oldest first.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(string set, CancellationToken cancellationToken = default);

    /// <summary>Lists runs matching <paramref name="query"/>, newest first.</summary>
    /// <param name="query">The filter: set, batch, status.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(EvalRunQuery query, CancellationToken cancellationToken = default);

    /// <summary>Cancels a queued or running run; a running one stops between items, keeping the items it scored. A run
    /// that already ended stays as it is.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run as it now stands.</returns>
    Task<Result<EvalRunModel>> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Rates one item's answer in one run; a later rating replaces it.</summary>
    /// <param name="rating">The rating.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalItemResult>> RateAsync(EvalRatingDraft rating, CancellationToken cancellationToken = default);

    /// <summary>Reports a set: every run, or the named ones.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="runIds">The runs to keep; none keeps every run.</param>
    /// <param name="withAnswers">Whether item rows carry the scored answer fields.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalReportModel>> ReportAsync(string set, IReadOnlyList<Guid>? runIds = null, bool withAnswers = false, CancellationToken cancellationToken = default);

    /// <summary>Exports a set with its items.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalSetExport>> ExportSetAsync(string set, CancellationToken cancellationToken = default);

    /// <summary>Imports a set with its items; items whose id exists are left as they are.</summary>
    /// <param name="export">The export.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalSetModel>> ImportSetAsync(EvalSetExport export, CancellationToken cancellationToken = default);
}
