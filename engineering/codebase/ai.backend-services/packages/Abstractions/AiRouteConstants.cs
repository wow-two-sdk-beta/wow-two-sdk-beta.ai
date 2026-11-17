namespace WoW.Two.Sdk.Beta.Be.Ai;

/// <summary>Holds the Ai HTTP API's routes, relative to the host's root, so the Hosting package maps them and the Client
/// package calls them from one list.</summary>
/// <remarks>Every route sits under <see cref="Root"/>; a value in braces is a path parameter, escaped by the caller.</remarks>
public static class AiRouteConstants
{
    /// <summary>Holds the prefix every route sits under.</summary>
    public const string Root = "api/ai";

    /// <summary>Holds <c>GET</c>: the provider catalog with priced models; <c>?live=true</c> adds each provider's live
    /// model ids.</summary>
    public const string Providers = Root + "/providers";

    /// <summary>Holds <c>GET</c>: every use case.</summary>
    public const string UseCases = Root + "/usecases";

    /// <summary>Holds <c>GET</c> and <c>PUT</c>: one use case.</summary>
    public const string UseCase = UseCases + "/{key}";

    /// <summary>Holds <c>GET</c> (newest version first) and <c>POST</c> (save): a use case's prompts.</summary>
    public const string Prompts = UseCase + "/prompts";

    /// <summary>Holds <c>GET</c>: the prompt a call uses, <c>?model=&amp;version=&amp;label=</c>.</summary>
    public const string PromptResolve = Prompts + "/resolve";

    /// <summary>Holds <c>GET</c>: a use case's labels.</summary>
    public const string Labels = UseCase + "/labels";

    /// <summary>Holds <c>PUT</c>: moves one label, body <c>{"version":2,"by":"…"}</c>.</summary>
    public const string Label = Labels + "/{label}";

    /// <summary>Holds <c>GET</c> and <c>PUT</c>: model prices.</summary>
    public const string Prices = Root + "/prices";

    /// <summary>Holds <c>POST</c>: one synchronous run of a use case.</summary>
    public const string Runs = Root + "/runs";

    /// <summary>Holds <c>GET</c>: the inference log, newest first.</summary>
    public const string Inferences = Root + "/inferences";

    /// <summary>Holds <c>GET</c>: the log summed per use case, provider and model, <c>?from=&amp;to=</c>.</summary>
    public const string InferenceTotals = Inferences + "/totals";

    /// <summary>Holds <c>POST</c>: clears the stored answers about one subject, body <c>{"subject":"…"}</c>.</summary>
    public const string InferenceForget = Inferences + "/forget";

    /// <summary>Holds <c>GET</c>: every eval set the caller sees.</summary>
    public const string EvalSets = Root + "/evals/sets";

    /// <summary>Holds <c>GET</c> and <c>PUT</c>: one eval set.</summary>
    public const string EvalSet = EvalSets + "/{key}";

    /// <summary>Holds <c>GET</c> and <c>POST</c>: a set's items; reading them needs <see cref="AiScopeConstants.EvalsManage"/>,
    /// since they hold inputs.</summary>
    public const string EvalSetItems = EvalSet + "/items";

    /// <summary>Holds <c>GET</c>: a set's runs, oldest first; <c>POST</c>: queues a matrix and answers <c>202</c> with the
    /// batch.</summary>
    public const string EvalSetRuns = EvalSet + "/runs";

    /// <summary>Holds <c>GET</c>: a set's report, <c>?runs=&amp;answers=&amp;format=json|csv|md</c>.</summary>
    public const string EvalSetReport = EvalSet + "/report";

    /// <summary>Holds <c>GET</c>: a set with its items, as an export.</summary>
    public const string EvalSetExport = EvalSet + "/export";

    /// <summary>Holds <c>POST</c>: imports an export.</summary>
    public const string EvalImport = Root + "/evals/import";

    /// <summary>Holds <c>GET</c> and <c>DELETE</c> (remove): one eval item.</summary>
    public const string EvalItem = Root + "/evals/items/{id}";

    /// <summary>Holds <c>PUT</c>: relabels an item, body <c>{"expected":"{…}","by":"…"}</c>.</summary>
    public const string EvalItemExpected = EvalItem + "/expected";

    /// <summary>Holds <c>GET</c>: runs, newest first, <c>?set=&amp;batch=&amp;status=&amp;limit=&amp;scope=</c>;
    /// <c>POST</c>: one run of a set, now (<c>EvalRunRequest</c>).</summary>
    public const string EvalRuns = Root + "/evals/runs";

    /// <summary>Holds <c>GET</c>: one run with its status and progress.</summary>
    public const string EvalRun = EvalRuns + "/{id}";

    /// <summary>Holds <c>POST</c>: cancels a queued or running run.</summary>
    public const string EvalRunCancel = EvalRun + "/cancel";

    /// <summary>Holds <c>POST</c>: rates one item of a run, body <c>{"itemId":"…","rating":4}</c>.</summary>
    public const string EvalRunRatings = EvalRun + "/ratings";

    /// <summary>Holds the query parameter that narrows a list to data scopes, repeatable; an empty value names
    /// <see cref="AiConstants.Unscoped"/>.</summary>
    public const string ScopeParameter = "scope";

    /// <summary>Fills a route's path parameters, escaping each value.</summary>
    /// <param name="route">A route of this class, such as <see cref="EvalRun"/>.</param>
    /// <param name="values">The values, by parameter name.</param>
    /// <returns>The path, with no leading slash.</returns>
    public static string Fill(string route, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(values);
        foreach (var (name, value) in values)
            route = route.Replace("{" + name + "}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        return route;
    }
}
