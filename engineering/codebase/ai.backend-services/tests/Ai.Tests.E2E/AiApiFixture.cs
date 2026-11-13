using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using WoW.Two.Sdk.Beta.Be.Testing.MultiHost;

namespace Ai.Tests.E2E;

/// <summary>Boots the real API over an isolated PostgreSQL container; the test host is a local run, so its boot applies the SQL migrations.</summary>
/// <remarks>Once the product's first table lands, override <c>InitializeStateAsync</c> with <c>postgres.InitializeRespawnerAsync()</c>, so <c>ResetAsync</c> empties it between tests; Respawn refuses a schema with no table.</remarks>
public sealed class AiApiFixture : MultiHostFixture, IAsyncLifetime
{
    /// <summary>Holds the public host the test target allows, beside <c>localhost</c>, as a deployed target's <c>AllowedHosts</c> does.</summary>
    public const string PublicHost = "ai.example.test";

    private readonly PostgresFixture postgres;

    public AiApiFixture()
    {
        postgres = AddSharedFixture(new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build()));
        Host = AddHost(new WebApiTestHost<Program>
        {
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
                ["AllowedHosts"] = PublicHost + ";localhost",
                ["Logging:LogLevel:Default"] = "Warning",
            }),
        });
    }

    /// <summary>Gets the booted API host.</summary>
    public WebApiTestHost<Program> Host { get; }

    Task IAsyncLifetime.InitializeAsync() => StartAsync().AsTask();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}

/// <summary>Shares one <see cref="AiApiFixture"/> across the whole E2E run.</summary>
[CollectionDefinition(Name)]
public sealed class AiApiCollection : ICollectionFixture<AiApiFixture>
{
    /// <summary>The collection name every E2E class joins.</summary>
    public const string Name = "ai-api";
}
