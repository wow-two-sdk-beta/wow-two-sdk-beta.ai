using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Reports;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Wire;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;
using static WoW.Two.Sdk.Beta.Be.Ai.Hosting.AiApiEndpointRouteBuilderExtensions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Endpoints;

/// <summary>Maps evals: sets, items, runs now or queued, status, cancel, ratings, reports, export and import, each inside
/// the caller's data scopes. A set, item or run outside them answers as missing.</summary>
internal static class EvalEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet(Relative(AiRouteConstants.EvalSets), (Delegate)ListSetsAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapGet(Relative(AiRouteConstants.EvalSet), (Delegate)GetSetAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapPut(Relative(AiRouteConstants.EvalSet), (Delegate)PutSetAsync).RequireAuthorization(AiApiPolicies.EvalsManage);

        group.MapGet(Relative(AiRouteConstants.EvalSetItems), (Delegate)ListItemsAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapPost(Relative(AiRouteConstants.EvalSetItems), (Delegate)AddItemAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapGet(Relative(AiRouteConstants.EvalItem), (Delegate)GetItemAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapDelete(Relative(AiRouteConstants.EvalItem), (Delegate)RemoveItemAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapPut(Relative(AiRouteConstants.EvalItemExpected), (Delegate)RelabelAsync).RequireAuthorization(AiApiPolicies.EvalsManage);

        group.MapGet(Relative(AiRouteConstants.EvalSetRuns), (Delegate)ListSetRunsAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapPost(Relative(AiRouteConstants.EvalSetRuns), (Delegate)QueueAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapGet(Relative(AiRouteConstants.EvalRuns), (Delegate)ListRunsAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapPost(Relative(AiRouteConstants.EvalRuns), (Delegate)RunNowAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapGet(Relative(AiRouteConstants.EvalRun), (Delegate)GetRunAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapPost(Relative(AiRouteConstants.EvalRunCancel), (Delegate)CancelAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapPost(Relative(AiRouteConstants.EvalRunRatings), (Delegate)RateAsync).RequireAuthorization(AiApiPolicies.EvalsManage);

        group.MapGet(Relative(AiRouteConstants.EvalSetReport), (Delegate)ReportAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapGet(Relative(AiRouteConstants.EvalSetExport), (Delegate)ExportAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
        group.MapPost(Relative(AiRouteConstants.EvalImport), (Delegate)ImportAsync).RequireAuthorization(AiApiPolicies.EvalsManage);
    }

    private static IEvalsBroker Evals(HttpContext context) => context.RequestServices.GetRequiredService<IEvalsBroker>();

    private static async Task<IResult> ListSetsAsync(HttpContext context)
    {
        var grant = await AiHttp.GrantAsync(context);
        return await Evals(context).ListSetsAsync(context.RequestAborted) switch
        {
            Result<IReadOnlyList<EvalSetModel>>.Success { Value: var sets } => AiHttp.Ok(sets.Where(set => grant.Allows(set.Scope)).ToList()),
            Result<IReadOnlyList<EvalSetModel>>.Failure { Error: var error } => AiHttp.Problem(context, error),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };
    }

    private static async Task<IResult> GetSetAsync(HttpContext context, string key)
    {
        var (_, problem) = await VisibleSetAsync(context, key);
        return problem ?? AiHttp.Reply(context, await Evals(context).GetSetAsync(key, context.RequestAborted));
    }

    private static async Task<IResult> PutSetAsync(HttpContext context, string key)
    {
        var (set, problem) = await AiHttp.ReadAsync<EvalSetModel>(context, body => AiHttp.Set(body, "key", key));
        if (problem is not null)
            return problem;

        var grant = await AiHttp.GrantAsync(context);
        return grant.TryWrite(set!.Scope, out var scope)
            ? AiHttp.Reply(context, await Evals(context).PutSetAsync(set with { Scope = scope }, context.RequestAborted))
            : AiHttp.ForbiddenScope(context);
    }

    private static async Task<IResult> ListItemsAsync(HttpContext context, string key)
    {
        var (_, problem) = await VisibleSetAsync(context, key);
        return problem ?? AiHttp.Reply(context, await Evals(context).ListItemsAsync(key, context.RequestAborted));
    }

    private static async Task<IResult> AddItemAsync(HttpContext context, string key)
    {
        var (_, hidden) = await VisibleSetAsync(context, key);
        if (hidden is not null)
            return hidden;

        var (draft, problem) = await AiHttp.ReadAsync<EvalItemDraft>(context, body => AiHttp.Set(body, "set", key));
        return problem ?? AiHttp.Reply(context, await Evals(context).AddItemAsync(draft!, context.RequestAborted));
    }

    private static async Task<IResult> GetItemAsync(HttpContext context, Guid id)
    {
        var (item, problem) = await VisibleItemAsync(context, id);
        return problem ?? AiHttp.Ok(item!);
    }

    private static async Task<IResult> RemoveItemAsync(HttpContext context, Guid id)
    {
        var (_, problem) = await VisibleItemAsync(context, id);
        return problem ?? AiHttp.Reply(context, await Evals(context).RemoveItemAsync(id, context.RequestAborted));
    }

    private static async Task<IResult> RelabelAsync(HttpContext context, Guid id)
    {
        var (_, hidden) = await VisibleItemAsync(context, id);
        if (hidden is not null)
            return hidden;

        var (relabel, problem) = await AiHttp.ReadAsync<EvalRelabelRequest>(context);
        return problem ?? AiHttp.Reply(context, await Evals(context).RelabelItemAsync(id, relabel!.Expected, relabel.By, context.RequestAborted));
    }

    private static async Task<IResult> ListSetRunsAsync(HttpContext context, string key)
    {
        var (_, problem) = await VisibleSetAsync(context, key);
        return problem ?? AiHttp.Reply(context, await Evals(context).ListRunsAsync(key, context.RequestAborted));
    }

    private static async Task<IResult> QueueAsync(HttpContext context, string key)
    {
        var (_, hidden) = await VisibleSetAsync(context, key);
        if (hidden is not null)
            return hidden;

        var (matrix, problem) = await AiHttp.ReadAsync<EvalMatrixRequest>(context, body => AiHttp.Set(body, "set", key));
        if (problem is not null)
            return problem;

        switch (await Evals(context).QueueAsync(matrix!, context.RequestAborted))
        {
            case Result<EvalBatchModel>.Success { Value: var batch }:
                context.Response.Headers.Location = $"/{AiRouteConstants.EvalRuns}?batch={batch.Id}";
                return AiHttp.Ok(batch, StatusCodes.Status202Accepted);
            case Result<EvalBatchModel>.Failure { Error: var error }:
                return AiHttp.Problem(context, error);
            default:
                throw new InvalidOperationException("A result is either a success or a failure.");
        }
    }

    private static async Task<IResult> ListRunsAsync(HttpContext context)
    {
        var grant = await AiHttp.GrantAsync(context);
        if (!grant.TryRead(AiHttp.Scopes(context), out var scopes))
            return AiHttp.ForbiddenScope(context);
        if (!InferenceEndpoints.TryGuid(context, "batch", out var batch) || !InferenceEndpoints.TryInt(context, "limit", out var limit))
            return AiHttp.Problem(context, AiErrors.Invalid("'batch' is an id and 'limit' a number."));

        EvalRunStatus? status = null;
        if (AiHttp.Query(context, "status") is { } text)
        {
            if (!Enum.TryParse<EvalRunStatus>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return AiHttp.Problem(context, AiErrors.Invalid("'status' is queued, running, succeeded, failed or canceled."));
            status = parsed;
        }

        var set = AiHttp.Query(context, "set");
        if (set is not null && await VisibleSetAsync(context, set) is (_, { } hidden))
            return hidden;

        return AiHttp.Reply(context, await Evals(context).ListRunsAsync(new EvalRunQuery
        {
            Set = set,
            BatchId = batch,
            Status = status,
            Limit = limit ?? 100,
            Scopes = scopes,
        }, context.RequestAborted));
    }

    private static async Task<IResult> RunNowAsync(HttpContext context)
    {
        var (request, problem) = await AiHttp.ReadAsync<EvalRunRequest>(context);
        if (problem is not null)
            return problem;

        var (_, hidden) = await VisibleSetAsync(context, request!.Set);
        return hidden ?? AiHttp.Reply(context, await Evals(context).RunAsync(request, context.RequestAborted));
    }

    private static async Task<IResult> GetRunAsync(HttpContext context, Guid id)
    {
        var (run, problem) = await VisibleRunAsync(context, id);
        return problem ?? AiHttp.Ok(run!);
    }

    private static async Task<IResult> CancelAsync(HttpContext context, Guid id)
    {
        var (_, problem) = await VisibleRunAsync(context, id);
        return problem ?? AiHttp.Reply(context, await Evals(context).CancelRunAsync(id, context.RequestAborted));
    }

    private static async Task<IResult> RateAsync(HttpContext context, Guid id)
    {
        var (_, hidden) = await VisibleRunAsync(context, id);
        if (hidden is not null)
            return hidden;

        var (rating, problem) = await AiHttp.ReadAsync<EvalRatingDraft>(context, body => AiHttp.Set(body, "runId", id.ToString()));
        return problem ?? AiHttp.Reply(context, await Evals(context).RateAsync(rating!, context.RequestAborted));
    }

    private static async Task<IResult> ReportAsync(HttpContext context, string key)
    {
        var (_, hidden) = await VisibleSetAsync(context, key);
        if (hidden is not null)
            return hidden;

        var format = (AiHttp.Query(context, "format") ?? "json").ToLowerInvariant();
        if (format is not ("json" or "csv" or "md"))
            return AiHttp.Problem(context, AiErrors.Invalid("'format' is json, csv or md."));

        var runIds = new List<Guid>();
        foreach (var text in AiHttp.QueryList(context, "runs"))
        {
            if (!Guid.TryParse(text, out var runId))
                return AiHttp.Problem(context, AiErrors.Invalid("'runs' is a comma list of run ids."));
            runIds.Add(runId);
        }

        var answers = string.Equals(AiHttp.Query(context, "answers"), "true", StringComparison.OrdinalIgnoreCase);
        return await Evals(context).ReportAsync(key, runIds, answers, context.RequestAborted) switch
        {
            Result<EvalReportModel>.Success { Value: var report } => format switch
            {
                "csv" => Results.Text(EvalReportFormats.Csv(report), "text/csv; charset=utf-8"),
                "md" => Results.Text(EvalReportFormats.Markdown(report), "text/markdown; charset=utf-8"),
                _ => AiHttp.Ok(report),
            },
            Result<EvalReportModel>.Failure { Error: var error } => AiHttp.Problem(context, error),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };
    }

    private static async Task<IResult> ExportAsync(HttpContext context, string key)
    {
        var (_, problem) = await VisibleSetAsync(context, key);
        return problem ?? AiHttp.Reply(context, await Evals(context).ExportSetAsync(key, context.RequestAborted));
    }

    private static async Task<IResult> ImportAsync(HttpContext context)
    {
        var (export, problem) = await AiHttp.ReadAsync<EvalSetExport>(context);
        if (problem is not null)
            return problem;

        var grant = await AiHttp.GrantAsync(context);
        return grant.TryWrite(export!.Set.Scope, out var scope)
            ? AiHttp.Reply(context, await Evals(context).ImportSetAsync(export with { Set = export.Set with { Scope = scope } }, context.RequestAborted))
            : AiHttp.ForbiddenScope(context);
    }

    /// <summary>Reads a set the caller sees; one outside its data scopes answers as missing.</summary>
    private static async Task<(EvalSetModel? Set, IResult? Problem)> VisibleSetAsync(HttpContext context, string key)
    {
        var grant = await AiHttp.GrantAsync(context);
        return await Evals(context).GetSetAsync(key, context.RequestAborted) switch
        {
            Result<EvalSetModel>.Success { Value: var set } when grant.Allows(set.Scope) => (set, null),
            Result<EvalSetModel>.Success => (null, AiHttp.Hidden(context, AiErrors.EvalSetNotFound(key))),
            Result<EvalSetModel>.Failure { Error: var error } => (null, AiHttp.Problem(context, error)),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };
    }

    /// <summary>Reads an item whose set the caller sees; one outside its data scopes answers as missing.</summary>
    private static async Task<(EvalItemModel? Item, IResult? Problem)> VisibleItemAsync(HttpContext context, Guid id)
    {
        switch (await Evals(context).GetItemAsync(id, context.RequestAborted))
        {
            case Result<EvalItemModel>.Success { Value: var item }:
                var (_, hidden) = await VisibleSetAsync(context, item.Set);
                return hidden is null ? (item, null) : (null, AiHttp.Hidden(context, AiErrors.EvalItemNotFound(id)));
            case Result<EvalItemModel>.Failure { Error: var error }:
                return (null, AiHttp.Problem(context, error));
            default:
                throw new InvalidOperationException("A result is either a success or a failure.");
        }
    }

    /// <summary>Reads a run the caller sees; one outside its data scopes answers as missing.</summary>
    private static async Task<(EvalRunModel? Run, IResult? Problem)> VisibleRunAsync(HttpContext context, Guid id)
    {
        var grant = await AiHttp.GrantAsync(context);
        return await Evals(context).GetRunAsync(id, context.RequestAborted) switch
        {
            Result<EvalRunModel>.Success { Value: var run } when grant.Allows(run.Scope) => (run, null),
            Result<EvalRunModel>.Success => (null, AiHttp.Hidden(context, AiErrors.EvalRunNotFound(id))),
            Result<EvalRunModel>.Failure { Error: var error } => (null, AiHttp.Problem(context, error)),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };
    }
}
