using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Migrations;

/// <summary>The module's embedded set over a fresh PostgreSQL, in its own schema: every migration applies once, and Apply → Rollback → Apply restores it.</summary>
/// <param name="fixture">The shared container, reset before each test.</param>
[Collection(MigratorCollection.Name)]
public sealed class MigrationTests(MigratorPostgresFixture fixture) : IAsyncLifetime
{
    private const string Set = AiModuleConstants.MigrationSetName;

    /// <inheritdoc />
    public async Task InitializeAsync() => await fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ApplyPending_ShouldApplyEveryMigrationOnce_InTheModulesSchema()
    {
        await using var migrator = CreateMigrator(fixture);
        var source = (await migrator.RunnerFor(Set).GetStatusAsync()).ValueOrThrow().Pending;

        var applied = (await migrator.RunnerFor(Set).ApplyPendingAsync("startup")).ValueOrThrow();

        source.Should().NotBeEmpty("the set ships its baseline at least");
        applied.Should().Equal(source.Select(migration => migration.Label));
        (await migrator.HasTableAsync(AiModuleConstants.SchemaName, $"{Set}_migration_history")).Should().BeTrue();
        var history = await migrator.ReadHistoryAsync(Set);
        history.Select(row => row.Ordinal).Should().Equal(source.Select(migration => migration.Ordinal));
        history.Should().OnlyContain(row => row.AppliedBy == "startup" && row.Checksum.Length == 64);
        (await migrator.RunnerFor(Set).ApplyPendingAsync("startup")).ValueOrThrow().Should().BeEmpty("a rerun finds every migration applied");
    }

    [Fact]
    public async Task ApplyPending_ShouldRestoreTheSchema_WhenEveryMigrationWasRolledBack()
    {
        await using var migrator = CreateMigrator(fixture, options => options.AllowRollback = true);
        var runner = migrator.RunnerFor(Set);
        var applied = (await runner.ApplyPendingAsync("test")).ValueOrThrow();

        (await runner.RollbackAsync(targetOrdinal: 0)).ValueOrThrow().Should().Equal(applied.Reverse());
        (await migrator.ReadHistoryAsync(Set)).Should().BeEmpty();

        (await runner.ApplyPendingAsync("test")).ValueOrThrow().Should().Equal(applied);
        var status = (await runner.GetStatusAsync()).ValueOrThrow();
        status.Applied.Should().HaveCount(applied.Count);
        status.Pending.Should().BeEmpty();
        status.Drifted.Should().BeEmpty();
    }

    /// <summary>The module's set as a Form 1 product's <c>AddAi</c> registers it: its name, its schema, its owner and its embedded SQL.</summary>
    internal static MigratorHarness CreateMigrator(MigratorPostgresFixture fixture, Action<MigrationOptions>? configure = null) =>
        MigratorHarness.CreatePostgres(fixture.ConnectionString, sets => sets.Add(Set, typeof(AiModuleAssembly).Assembly, options =>
        {
            options.SchemaName = AiModuleConstants.SchemaName;
            options.SchemaOwner = AiModuleConstants.ModuleSchemaOwner;
            configure?.Invoke(options);
        }));
}
