namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting;

/// <summary>Holds the settings of the Ai HTTP API (<c>AddAiApi</c>).</summary>
public sealed class AiApiOptions
{
    /// <summary>Holds the configuration section a host binds these settings from.</summary>
    public const string SectionName = "Ai:Api";

    /// <summary>Gets or sets whether every route answers without a token, in the Development environment only: a local
    /// bench with no Identity. Outside Development a host that sets it fails to start.</summary>
    /// <remarks>Data scopes still apply: <see cref="Access.IAiScopeAccess"/> decides what an anonymous caller sees.</remarks>
    public bool AllowAnonymousInDevelopment { get; set; }
}
