namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

/// <summary>Holds the authorization policies <c>AddAiApi</c> registers, one per <see cref="AiScopeConstants"/> scope,
/// named apart from a host's own policies.</summary>
public static class AiApiPolicies
{
    /// <summary>Holds the prefix every policy name starts with.</summary>
    public const string Prefix = "ai-api:";

    /// <summary>Holds the policy that needs <see cref="AiScopeConstants.Read"/>.</summary>
    public const string Read = Prefix + AiScopeConstants.Read;

    /// <summary>Holds the policy that needs <see cref="AiScopeConstants.Run"/>.</summary>
    public const string Run = Prefix + AiScopeConstants.Run;

    /// <summary>Holds the policy that needs <see cref="AiScopeConstants.PromptsManage"/>.</summary>
    public const string PromptsManage = Prefix + AiScopeConstants.PromptsManage;

    /// <summary>Holds the policy that needs <see cref="AiScopeConstants.EvalsManage"/>.</summary>
    public const string EvalsManage = Prefix + AiScopeConstants.EvalsManage;

    /// <summary>Gets the policy name for a scope.</summary>
    /// <param name="scope">One of <see cref="AiScopeConstants.All"/>.</param>
    public static string For(string scope) => Prefix + scope;
}
