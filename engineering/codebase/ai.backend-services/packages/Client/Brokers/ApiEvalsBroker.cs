using System.Globalization;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Keeps eval sets and runs them on the Ai service, within the data scopes it grants the caller: a set, item or run
/// outside them reads as missing.</summary>
/// <remarks><see cref="RunAsync"/> waits for the whole run on one request; queue a large one (<see cref="QueueAsync"/>) and
/// read its status instead.</remarks>
/// <param name="transport">The API transport.</param>
internal sealed class ApiEvalsBroker(AiApiTransport transport) : IEvalsBroker
{
    /// <inheritdoc />
    public Task<Result<EvalSetModel>> PutSetAsync(EvalSetModel set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Guard<EvalSetModel>(set.Key) ?? transport.SendAsync<EvalSetModel>(HttpMethod.Put, Set(set.Key), set, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<EvalSetModel>>> ListSetsAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<IReadOnlyList<EvalSetModel>>(AiRouteConstants.EvalSets, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalSetModel>> GetSetAsync(string set, CancellationToken cancellationToken = default) =>
        Guard<EvalSetModel>(set) ?? transport.GetAsync<EvalSetModel>(Set(set), cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalItemModel>> AddItemAsync(EvalItemDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Guard<EvalItemModel>(draft.Set)
            ?? transport.SendAsync<EvalItemModel>(HttpMethod.Post, AiRouteConstants.Fill(AiRouteConstants.EvalSetItems, ("key", draft.Set)), draft, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalItemModel>> RelabelItemAsync(Guid itemId, string expected, string? by = null, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EvalItemModel>(HttpMethod.Put, Item(AiRouteConstants.EvalItemExpected, itemId), new Relabel(expected, by), cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        transport.SendAsync<bool>(HttpMethod.Delete, Item(AiRouteConstants.EvalItem, itemId), body: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalItemModel>> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        transport.GetAsync<EvalItemModel>(Item(AiRouteConstants.EvalItem, itemId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<EvalItemModel>>> ListItemsAsync(string set, CancellationToken cancellationToken = default) =>
        Guard<IReadOnlyList<EvalItemModel>>(set)
            ?? transport.GetAsync<IReadOnlyList<EvalItemModel>>(AiRouteConstants.Fill(AiRouteConstants.EvalSetItems, ("key", set)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalRunModel>> RunAsync(EvalRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Guard<EvalRunModel>(request.Set) ?? transport.SendAsync<EvalRunModel>(HttpMethod.Post, AiRouteConstants.EvalRuns, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalBatchModel>> QueueAsync(EvalMatrixRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Guard<EvalBatchModel>(request.Set)
            ?? transport.SendAsync<EvalBatchModel>(HttpMethod.Post, AiRouteConstants.Fill(AiRouteConstants.EvalSetRuns, ("key", request.Set)), request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalRunModel>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        transport.GetAsync<EvalRunModel>(Run(AiRouteConstants.EvalRun, runId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(string set, CancellationToken cancellationToken = default) =>
        Guard<IReadOnlyList<EvalRunModel>>(set)
            ?? transport.GetAsync<IReadOnlyList<EvalRunModel>>(AiRouteConstants.Fill(AiRouteConstants.EvalSetRuns, ("key", set)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<EvalRunModel>>> ListRunsAsync(EvalRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Scopes is { Count: 0 })
            return Task.FromResult(Result<IReadOnlyList<EvalRunModel>>.Ok([]));

        var path = AiApiTransport.Path(AiRouteConstants.EvalRuns, [
            ("set", query.Set), ("batch", query.BatchId?.ToString()),
            ("status", query.Status is { } status ? JsonNamingPolicy.CamelCase.ConvertName(status.ToString()) : null),
            ("limit", query.Limit.ToString(CultureInfo.InvariantCulture)), .. AiApiTransport.Scopes(query.Scopes)]);
        return transport.GetAsync<IReadOnlyList<EvalRunModel>>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalRunModel>> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EvalRunModel>(HttpMethod.Post, Run(AiRouteConstants.EvalRunCancel, runId), body: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalItemResult>> RateAsync(EvalRatingDraft rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);
        return transport.SendAsync<EvalItemResult>(HttpMethod.Post, Run(AiRouteConstants.EvalRunRatings, rating.RunId), rating, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalReportModel>> ReportAsync(string set, IReadOnlyList<Guid>? runIds = null, bool withAnswers = false, CancellationToken cancellationToken = default)
    {
        if (Guard<EvalReportModel>(set) is { } refused)
            return refused;

        var path = AiApiTransport.Path(AiRouteConstants.Fill(AiRouteConstants.EvalSetReport, ("key", set)),
            ("runs", runIds is { Count: > 0 } ? string.Join(',', runIds) : null), ("answers", withAnswers ? "true" : null), ("format", "json"));
        return transport.GetAsync<EvalReportModel>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EvalSetExport>> ExportSetAsync(string set, CancellationToken cancellationToken = default) =>
        Guard<EvalSetExport>(set) ?? transport.GetAsync<EvalSetExport>(AiRouteConstants.Fill(AiRouteConstants.EvalSetExport, ("key", set)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<EvalSetModel>> ImportSetAsync(EvalSetExport export, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        return transport.SendAsync<EvalSetModel>(HttpMethod.Post, AiRouteConstants.EvalImport, export, cancellationToken);
    }

    private static string Set(string key) => AiRouteConstants.Fill(AiRouteConstants.EvalSet, ("key", key));

    private static string Item(string route, Guid id) => AiRouteConstants.Fill(route, ("id", id.ToString()));

    private static string Run(string route, Guid id) => AiRouteConstants.Fill(route, ("id", id.ToString()));

    /// <summary>Refuses a blank set key before a call; none when the key can be sent.</summary>
    private static Task<Result<T>>? Guard<T>(string? set) where T : notnull =>
        AiApiTransport.Blank(set, "set key") is { } error ? Task.FromResult(Result<T>.Fail(error)) : null;

    /// <summary>Represents the body of a relabel.</summary>
    private sealed record Relabel(string Expected, string? By);
}
