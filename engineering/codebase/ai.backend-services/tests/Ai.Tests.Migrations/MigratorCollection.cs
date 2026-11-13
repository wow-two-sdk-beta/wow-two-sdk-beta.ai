using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Migrations;

/// <summary>xUnit collection over the SDK <see cref="MigratorPostgresFixture"/>; its reset drops every schema, so each test applies from scratch.</summary>
[CollectionDefinition(Name)]
public sealed class MigratorCollection : ICollectionFixture<MigratorPostgresFixture>
{
    /// <summary>The collection name every migrator test class joins.</summary>
    public const string Name = "postgres-migrator";
}
