using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Ai.Tests.Conformance.Prompts;

/// <summary>Form 1: the module wired as a consumer wires it, over a PostgreSQL container its own set builds; the scripted
/// caller stands in for the providers, which the prompt registry never calls.</summary>
public sealed class ModuleFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());

    /// <summary>Gets the consumer's container.</summary>
    public ServiceProvider Services { get; private set; } = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection().AddLogging().AddSingleton<IAiModelCaller>(new ScriptedAiModelCaller());
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
