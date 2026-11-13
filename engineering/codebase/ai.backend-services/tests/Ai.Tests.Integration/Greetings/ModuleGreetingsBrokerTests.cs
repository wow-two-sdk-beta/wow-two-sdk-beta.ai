using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Ai.Greetings;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Greetings;

/// <summary>The module wired as a consumer wires it, over a real PostgreSQL its own set builds in its own schema.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class ModuleGreetingsBrokerTests(MigratorPostgresFixture fixture) : IClassFixture<MigratorPostgresFixture>, IAsyncLifetime
{
    private readonly FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch.AddDays(1));
    private ServiceProvider services = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        var collection = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(clock);
        collection.AddAi().Configure(options => options.ConnectionString = fixture.ConnectionString);
        services = collection.BuildServiceProvider(validateScopes: true);

        // The consumer's deploy step: every registered set, the module's included.
        (await services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    public async Task DisposeAsync() => await services.DisposeAsync();

    [Fact]
    public async Task AddAi_ShouldApplyItsSet_InTheModulesOwnSchema()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);

        var tables = (await connection.QueryAsync<string>(
            "select table_schema || '.' || table_name from information_schema.tables where table_schema in ('public', @schema)",
            new { schema = AiModuleConstants.SchemaName })).ToList();

        tables.Should().Contain($"{AiModuleConstants.SchemaName}.{AiModuleConstants.MigrationSetName}_migration_history")
            .And.NotContain(table => table.StartsWith("public.", StringComparison.Ordinal), "the module writes nothing into the consumer's schema");
    }

    [Fact]
    public async Task GreetAsync_ShouldStampTheGreeting_WhenItIsServed()
    {
        await using var scope = services.CreateAsyncScope();

        var greeting = (await scope.ServiceProvider.GetRequiredService<IGreetingsBroker>().GreetAsync("Ai")).ValueOrThrow();

        greeting.Message.Should().Be("Hello, Ai!");
        greeting.GreetedAtUtc.Should().Be(clock.GetUtcNow());
    }

    [Fact]
    public async Task GreetAsync_ShouldRefuse_WhenTheNameIsTooLong()
    {
        await using var scope = services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IGreetingsBroker>().GreetAsync(new string('a', GreetingConstants.MaxNameLength + 1));

        result.Should().BeOfType<Result<GreetingModel>.Failure>();
    }
}
