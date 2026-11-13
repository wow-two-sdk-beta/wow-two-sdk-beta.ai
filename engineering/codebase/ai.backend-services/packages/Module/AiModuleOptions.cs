namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Holds the settings of the in-process Ai module.</summary>
public sealed class AiModuleOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string of the database that holds the module's schema.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
