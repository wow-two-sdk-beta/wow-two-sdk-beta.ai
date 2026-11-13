using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Ai.Tests.Conformance.Greetings;

/// <summary>Form 1: the module wired as a consumer wires it, over a PostgreSQL container its own set builds.</summary>
public sealed class ModuleFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());

    /// <summary>Gets the clock the module stamps greetings with.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UnixEpoch.AddDays(1));

    /// <summary>Gets the consumer's container.</summary>
    public ServiceProvider Services { get; private set; } = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(Clock);
        services.AddAi().Configure(options => options.ConnectionString = postgres.ConnectionString);
        Services = services.BuildServiceProvider(validateScopes: true);

        // The consumer's deploy step: every registered set, the module's included.
        (await Services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
