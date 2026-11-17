using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Reports;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Wire;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using static WoW.Two.Sdk.Beta.Be.Ai.Hosting.AiApiEndpointRouteBuilderExtensions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Endpoints;

/// <summary>Maps the shared catalog: providers, use cases, prompts, labels and prices. One prompt serves every data scope,
/// so none of these routes filters by scope.</summary>
internal static class CatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet(Relative(AiRouteConstants.Providers), (Delegate)ProvidersAsync).RequireAuthorization(AiApiPolicies.Read);

        group.MapGet(Relative(AiRouteConstants.UseCases), async (HttpContext context) =>
            AiHttp.Reply(context, await Prompts(context).ListUseCasesAsync(context.RequestAborted))).RequireAuthorization(AiApiPolicies.Read);
        group.MapGet(Relative(AiRouteConstants.UseCase), async (HttpContext context, string key) =>
            AiHttp.Reply(context, await Prompts(context).GetUseCaseAsync(key, context.RequestAborted))).RequireAuthorization(AiApiPolicies.Read);
        group.MapPut(Relative(AiRouteConstants.UseCase), (Delegate)PutUseCaseAsync).RequireAuthorization(AiApiPolicies.PromptsManage);

        group.MapGet(Relative(AiRouteConstants.Prompts), async (HttpContext context, string key) =>
            AiHttp.Reply(context, await Prompts(context).ListPromptsAsync(key, context.RequestAborted))).RequireAuthorization(AiApiPolicies.Read);
        group.MapPost(Relative(AiRouteConstants.Prompts), (Delegate)SavePromptAsync).RequireAuthorization(AiApiPolicies.PromptsManage);
        group.MapGet(Relative(AiRouteConstants.PromptResolve), (Delegate)ResolveAsync).RequireAuthorization(AiApiPolicies.Read);

        group.MapGet(Relative(AiRouteConstants.Labels), async (HttpContext context, string key) =>
            AiHttp.Reply(context, await Prompts(context).ListLabelsAsync(key, context.RequestAborted))).RequireAuthorization(AiApiPolicies.Read);
        group.MapPut(Relative(AiRouteConstants.Label), (Delegate)MoveLabelAsync).RequireAuthorization(AiApiPolicies.PromptsManage);

        group.MapGet(Relative(AiRouteConstants.Prices), async (HttpContext context) =>
            AiHttp.Reply(context, await Prices(context).ListAsync(context.RequestAborted))).RequireAuthorization(AiApiPolicies.Read);
        group.MapPut(Relative(AiRouteConstants.Prices), (Delegate)PutPriceAsync).RequireAuthorization(AiApiPolicies.PromptsManage);
    }

    private static IPromptsBroker Prompts(HttpContext context) => context.RequestServices.GetRequiredService<IPromptsBroker>();

    private static IModelPricesBroker Prices(HttpContext context) => context.RequestServices.GetRequiredService<IModelPricesBroker>();

    private static async Task<IResult> ProvidersAsync(HttpContext context)
    {
        var live = string.Equals(AiHttp.Query(context, "live"), "true", StringComparison.OrdinalIgnoreCase);
        var overview = await AiProviderOverview.BuildAsync(context.RequestServices.GetService<IAiProviderCatalog>(), Prices(context), live, context.RequestAborted);
        return AiHttp.Ok(overview);
    }

    private static async Task<IResult> PutUseCaseAsync(HttpContext context, string key)
    {
        var (useCase, problem) = await AiHttp.ReadAsync<UseCaseModel>(context, body => AiHttp.Set(body, "key", key));
        return problem ?? AiHttp.Reply(context, await Prompts(context).PutUseCaseAsync(useCase!, context.RequestAborted));
    }

    private static async Task<IResult> SavePromptAsync(HttpContext context, string key)
    {
        var (draft, problem) = await AiHttp.ReadAsync<PromptDraft>(context, body => AiHttp.Set(body, "useCase", key));
        return problem ?? AiHttp.Reply(context, await Prompts(context).SavePromptAsync(draft!, context.RequestAborted));
    }

    private static async Task<IResult> ResolveAsync(HttpContext context, string key)
    {
        if (AiHttp.Query(context, "model") is not { } model)
            return AiHttp.Problem(context, AiErrors.Invalid("'model' names the model to resolve the prompt for."));
        int? version = null;
        if (AiHttp.Query(context, "version") is { } text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return AiHttp.Problem(context, AiErrors.Invalid("'version' is a number."));
            version = parsed;
        }

        return AiHttp.Reply(context, await Prompts(context).ResolveAsync(key, model, version, AiHttp.Query(context, "label"), context.RequestAborted));
    }

    private static async Task<IResult> MoveLabelAsync(HttpContext context, string key, string label)
    {
        var (move, problem) = await AiHttp.ReadAsync<AiLabelMoveRequest>(context);
        return problem ?? AiHttp.Reply(context, await Prompts(context).MoveLabelAsync(key, label, move!.Version, move.By, context.RequestAborted));
    }

    private static async Task<IResult> PutPriceAsync(HttpContext context)
    {
        var (price, problem) = await AiHttp.ReadAsync<ModelPriceModel>(context);
        return problem ?? AiHttp.Reply(context, await Prices(context).PutAsync(price!, context.RequestAborted));
    }
}
