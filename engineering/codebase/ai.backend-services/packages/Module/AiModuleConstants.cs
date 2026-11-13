namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Holds the names the module registers under, so a Form 1 host's own registrations never collide.</summary>
public static class AiModuleConstants
{
    /// <summary>Holds the module's migration set; its history lives in <c>ai_migration_history</c> in its schema.</summary>
    public const string MigrationSetName = "ai";

    /// <summary>Holds the schema the set owns: its tables and its history, apart from the host's <c>public</c>.</summary>
    public const string SchemaName = MigrationSetName;

    /// <summary>Holds the owner kind a Form 1 product claims the schema as, hosting the module over its own database.</summary>
    public const string ModuleSchemaOwner = "module";

    /// <summary>Holds the owner kind the Ai service claims the schema as, an instance serving Forms 2 and 3.</summary>
    public const string ServiceSchemaOwner = "service";

    /// <summary>Holds the key of the module's own keyed services: its Npgsql data source, connection factory and database
    /// settings. It equals <see cref="MigrationSetName"/>, because the migrator opens and creates a set's database from the
    /// services keyed by its name.</summary>
    public const string ServiceKey = MigrationSetName;
}
