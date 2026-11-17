using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Endpoints;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting;

/// <summary>Maps the Ai HTTP API.</summary>
public static class AiApiEndpointRouteBuilderExtensions
{
    /// <summary>Maps every route under <see cref="AiRouteConstants.Root"/> (<c>api/ai/</c>): providers, use cases, prompts,
    /// labels, prices, synchronous runs, the inference log with totals and purges, eval sets, items, runs now or queued
    /// (<c>202</c> with the batch), status, cancel, ratings, reports as json, csv or md, export and import.</summary>
    /// <remarks>
    ///   - needs <c>AddAiApi()</c>, the contract's brokers, and the host's authentication and authorization middleware
    ///     before the endpoints
    ///   - each route requires one <see cref="AiScopeConstants"/> scope (<see cref="Access.AiApiPolicies"/>); routes that
    ///     return item inputs need <see cref="AiScopeConstants.EvalsManage"/>, never the generic read
    ///   - success bodies carry the payload under <c>data</c>; failures are the be sdk's problem details, <c>code</c> naming
    ///     the error type
    ///   - data scopes come from the host's <see cref="Access.IAiScopeAccess"/>: lists keep the caller's scopes, a run,
    ///     set or item outside them answers 404, and a write naming another scope 403
    /// </remarks>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The route group, for a host's own conventions (rate limits, tags).</returns>
    public static RouteGroupBuilder MapAiApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/" + AiRouteConstants.Root).WithTags("Ai");
        CatalogEndpoints.Map(group);
        RunEndpoints.Map(group);
        InferenceEndpoints.Map(group);
        EvalEndpoints.Map(group);
        return group;
    }

    /// <summary>Gets a route relative to the group's root.</summary>
    /// <param name="route">A route of <see cref="AiRouteConstants"/>.</param>
    internal static string Relative(string route) => route[(AiRouteConstants.Root.Length + 1)..];
}
