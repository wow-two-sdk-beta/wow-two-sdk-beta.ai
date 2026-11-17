namespace WoW.Two.Sdk.Beta.Be.Ai;

/// <summary>Holds the OAuth scopes the Ai HTTP API accepts: what the calling app may ask of it. A token lists every scope
/// its client may use on the API; each route names the one it needs.</summary>
/// <remarks>These are permissions, not data scopes: which tenants' rows a caller sees is the host's
/// <c>IAiScopeAccess</c> (Hosting package), over the <c>Scope</c> each call, set and run carries.</remarks>
public static class AiScopeConstants
{
    /// <summary>Holds the scope that reads providers, use cases, prompts, labels, prices, inferences, totals, eval sets,
    /// runs and reports.</summary>
    public const string Read = "ai.read";

    /// <summary>Holds the scope that runs a use case and purges a subject's stored answers.</summary>
    public const string Run = "ai.run";

    /// <summary>Holds the scope that writes use cases, prompts, labels and prices.</summary>
    public const string PromptsManage = "ai.prompts-manage";

    /// <summary>Holds the scope that writes eval sets and items, reads their inputs, queues, cancels and rates runs, and
    /// exports and imports sets.</summary>
    public const string EvalsManage = "ai.evals-manage";

    /// <summary>Gets every scope, in the order the API documents them.</summary>
    public static IReadOnlyList<string> All { get; } = [Read, Run, PromptsManage, EvalsManage];
}
