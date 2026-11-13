using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations.Lint;

namespace Ai.Tests.Migrations;

/// <summary>The rules every file of the module's set ships under: the be sdk lint and idempotent scripts (M22, M59).</summary>
/// <param name="fixture">The shared container, reset before each test.</param>
[Collection(MigratorCollection.Name)]
public sealed class MigrationRulesTests(MigratorPostgresFixture fixture) : IAsyncLifetime
{
    /// <summary>The last ordinal the expand-only rule skips; every later file expands, or carries <c>-- @contract since=vX.Y</c>.</summary>
    private const int ExpandOnlyCutoff = 0;

    /// <summary>The last ordinal the idempotency proof grandfathers; raise it only for files applied before the rule.</summary>
    private const int IdempotentAfter = 0;

    /// <inheritdoc />
    public async Task InitializeAsync() => await fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Lint_ShouldFindNothing_InTheSetOrTheModel()
    {
        // The set exactly as the module registers it, and the model its context maps.
        var services = new ServiceCollection().AddLogging();
        services.AddAi().Configure(options => options.ConnectionString = fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<AiDbContext>().Model;

        await MigrationLint.AssertCleanAsync(provider, lint => lint
            .ExpandOnlyAfter(AiModuleConstants.MigrationSetName, ExpandOnlyCutoff)
            .CheckModel(AiModuleConstants.MigrationSetName, model));
    }

    [Fact]
    public async Task Scripts_ShouldBeIdempotent_WhenEachRunsTwice()
    {
        await using var migrator = MigrationTests.CreateMigrator(fixture);

        await migrator.AssertIdempotentAsync(AiModuleConstants.MigrationSetName, after: IdempotentAfter);
    }
}
