using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Wire;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using static WoW.Two.Sdk.Beta.Be.Ai.Hosting.AiApiEndpointRouteBuilderExtensions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Endpoints;

/// <summary>Maps one synchronous run of a use case, in the caller's data scope.</summary>
internal static class RunEndpoints
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(Relative(AiRouteConstants.Runs), (Delegate)RunAsync).RequireAuthorization(AiApiPolicies.Run);

    private static async Task<IResult> RunAsync(HttpContext context)
    {
        var (request, problem) = await AiHttp.ReadAsync<AiRunRequest>(context);
        if (problem is not null)
            return problem;

        var grant = await AiHttp.GrantAsync(context);
        if (!grant.TryWrite(request!.Scope, out var scope))
            return AiHttp.ForbiddenScope(context);

        // An eval run's calls come from the eval broker only; a caller never files a call under one.
        var runner = context.RequestServices.GetRequiredService<IAiRunner>();
        return AiHttp.Reply(context, await runner.RunAsync(request with { Scope = scope, EvalRunId = null }, context.RequestAborted));
    }
}

/// <summary>Maps the inference log: lists, totals and a subject's purge, each within the caller's data scopes.</summary>
internal static class InferenceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet(Relative(AiRouteConstants.Inferences), (Delegate)ListAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapGet(Relative(AiRouteConstants.InferenceTotals), (Delegate)TotalsAsync).RequireAuthorization(AiApiPolicies.Read);
        group.MapPost(Relative(AiRouteConstants.InferenceForget), (Delegate)ForgetAsync).RequireAuthorization(AiApiPolicies.Run);
    }

    private static IInferencesBroker Inferences(HttpContext context) => context.RequestServices.GetRequiredService<IInferencesBroker>();

    private static async Task<IResult> ListAsync(HttpContext context)
    {
        var grant = await AiHttp.GrantAsync(context);
        if (!grant.TryRead(AiHttp.Scopes(context), out var scopes))
            return AiHttp.ForbiddenScope(context);
        if (!TryGuid(context, "evalRunId", out var run) || !TryInstant(context, "from", out var from) || !TryInstant(context, "to", out var to)
            || !TryInt(context, "limit", out var limit))
            return AiHttp.Problem(context, AiErrors.Invalid("'evalRunId' is an id, 'from' and 'to' are instants, 'limit' is a number."));

        return AiHttp.Reply(context, await Inferences(context).ListAsync(new InferenceQuery
        {
            UseCase = AiHttp.Query(context, "useCase"),
            Subject = AiHttp.Query(context, "subject"),
            EvalRunId = run,
            FromUtc = from,
            ToUtc = to,
            Limit = limit ?? 100,
            Scopes = scopes,
        }, context.RequestAborted));
    }

    private static async Task<IResult> TotalsAsync(HttpContext context)
    {
        var grant = await AiHttp.GrantAsync(context);
        if (!grant.TryRead(AiHttp.Scopes(context), out var scopes))
            return AiHttp.ForbiddenScope(context);
        if (!TryInstant(context, "from", out var from) || !TryInstant(context, "to", out var to) || from is null || to is null)
            return AiHttp.Problem(context, AiErrors.Invalid("'from' and 'to' are instants, both required."));

        return AiHttp.Reply(context, await Inferences(context).TotalsAsync(
            new InferenceTotalsQuery { FromUtc = from.Value, ToUtc = to.Value, Scopes = scopes }, context.RequestAborted));
    }

    private static async Task<IResult> ForgetAsync(HttpContext context)
    {
        var (request, problem) = await AiHttp.ReadAsync<AiForgetRequest>(context);
        if (problem is not null)
            return problem;

        // A purge clears the subject in the scopes the caller names, else in every scope it sees; never in another tenant's.
        var grant = await AiHttp.GrantAsync(context);
        if (!grant.TryRead(AiHttp.Scopes(context), out var scopes))
            return AiHttp.ForbiddenScope(context);
        return AiHttp.Reply(context, await Inferences(context).ForgetSubjectAsync(request!.Subject, scopes, context.RequestAborted));
    }

    internal static bool TryGuid(HttpContext context, string name, out Guid? value)
    {
        value = null;
        if (AiHttp.Query(context, name) is not { } text)
            return true;
        if (!Guid.TryParse(text, out var parsed))
            return false;
        value = parsed;
        return true;
    }

    internal static bool TryInt(HttpContext context, string name, out int? value)
    {
        value = null;
        if (AiHttp.Query(context, name) is not { } text)
            return true;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;
        value = parsed;
        return true;
    }

    internal static bool TryInstant(HttpContext context, string name, out DateTimeOffset? value)
    {
        value = null;
        if (AiHttp.Query(context, name) is not { } text)
            return true;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return false;
        value = parsed;
        return true;
    }
}
