using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Keeps eval sets and runs them: each run sends every item through one prompt version on one model, scores
/// the answers, and records them in the inference log, so one report joins accuracy, tokens, cost and ratings.</summary>
public interface IEvalsBroker
{
    /// <summary>Creates or replaces a set's definition; its items stay.</summary>
    /// <param name="set">The set.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalSetModel>> PutSetAsync(EvalSetModel set, CancellationToken cancellationToken = default);

    /// <summary>Lists every set.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalSetModel>>> ListSetsAsync(CancellationToken cancellationToken = default);

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

    /// <summary>Lists a set's items.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalItemModel>>> ListItemsAsync(string set, CancellationToken cancellationToken = default);

    /// <summary>Runs a set through one prompt version on one model and scores every item.</summary>
    /// <param name="request">The run.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<EvalRunModel>> RunAsync(EvalRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists a set's runs, oldest first.</summary>
    /// <param name="set">The set key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(string set, CancellationToken cancellationToken = default);

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
