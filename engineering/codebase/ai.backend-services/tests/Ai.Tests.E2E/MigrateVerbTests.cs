using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Ai.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations.Verbs;

namespace Ai.Tests.E2E;

/// <summary>
/// The image's <c>migrate</c> verbs (<c>dotnet Ai.Api.dll migrate &lt;verb&gt;</c>) over the services its
/// <c>Program</c> builds, on a fresh database each: the exits, reruns that change nothing, a write-free <c>check</c> and
/// the <c>migrate-result</c> line (M57). The host never starts, as in the deploy's migrate step. The image hosts one set,
/// the module's.
/// </summary>
/// <param name="fixture">A container of its own, so the API host's connections and locks never reach the snapshots; reset before each test.</param>
public sealed class MigrateVerbTests(MigratorPostgresFixture fixture) : IClassFixture<MigratorPostgresFixture>, IAsyncLifetime
{
    private WebApplication app = null!;

    /// <summary>The fresh database's name, which a guarded verb's <c>--confirm</c> names.</summary>
    private string Database => new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Database!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        app = Build(fixture.ConnectionString);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task Check_ShouldWriteNothing_OnAFreshAndOnAMigratedDatabase()
    {
        var verbs = Verbs();

        var fresh = await verbs.AssertCheckWritesNothingAsync();
        fresh.Exit.Should().Be(1, "a fresh database reads the set behind");
        var pending = Module(fresh.Result).Pending.Select(label => label.Label).ToList();
        Module(fresh.Result).State.Should().Be(MigrationLevelState.Behind);
        pending.Should().NotBeEmpty();

        var migrate = await verbs.AssertExitAsync(0);
        Module(migrate.Result).Applied.Select(label => label.Label).Should().Equal(pending);

        var current = await verbs.AssertCheckWritesNothingAsync();
        current.Exit.Should().Be(0, current.ToString());
        Module(current.Result).State.Should().Be(MigrationLevelState.Current);
    }

    [Fact]
    public async Task EveryRunnableVerb_ShouldRerunWithoutAChange()
    {
        var verbs = Verbs();

        await verbs.AssertRerunChangesNothingAsync("apply", "--dry-run");
        await verbs.AssertRerunChangesNothingAsync("apply");
        await verbs.AssertRerunChangesNothingAsync("seed");
        await verbs.AssertRerunChangesNothingAsync();
        foreach (var read in new[] { "check", "plan", "locks", "sets" })
            await verbs.AssertRerunChangesNothingAsync(read);
    }

    [Fact]
    public async Task Rollback_ShouldRefuseWithoutTheDatabaseName_ThenRollBackAndApplyAgain()
    {
        var verbs = Verbs();
        var applied = Module((await verbs.AssertExitAsync(0, "apply")).Result).Applied.Select(label => label.Label).ToList();

        (await verbs.AssertExitAsync(1, "rollback", "--to", "0")).Result.Message.Should().Contain("--confirm");
        await verbs.AssertExitAsync(1, "rollback", "--to", "0", "--confirm", "another_database");
        Module((await verbs.AssertCheckWritesNothingAsync()).Result).State.Should().Be(MigrationLevelState.Current, "a refused rollback runs no SQL");

        await verbs.AssertExitAsync(0, "rollback", "--to", "0", "--confirm", Database);
        await verbs.AssertRerunChangesNothingAsync("rollback", "--to", "0", "--confirm", Database);
        Module((await verbs.AssertExitAsync(1, "check")).Result).State.Should().Be(MigrationLevelState.Behind);

        Module((await verbs.AssertExitAsync(0, "apply")).Result).Applied.Select(label => label.Label).Should().Equal(applied);
    }

    [Fact]
    public async Task Sets_ShouldDescribeTheModulesSet_WithNoDatabaseSettings()
    {
        // The pipelines generator runs `migrate sets --json` on the built image with no settings file and no database (M78).
        await using var bare = Build(connectionString: null);

        var sets = await new MigrateVerbHarness(bare.Services, _ => throw new InvalidOperationException("sets opens no database"))
            .RunAsync("sets", "--json");

        sets.Exit.Should().Be(0, sets.ToString());
        sets.Result.Verb.Should().Be("sets");
        Module(sets.Result).Schema.Should().Be(AiModuleConstants.SchemaName);
    }

    /// <summary>Builds the services as <c>Program</c> does — the configured builder, built, never started — over <paramref name="connectionString"/>.</summary>
    private static WebApplication Build(string? connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:ConnectionString"] = connectionString,
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.Configure();
        return builder.Build();
    }

    /// <summary>The verb harness over the built host's services, snapshotting the fresh database.</summary>
    private MigrateVerbHarness Verbs() => new(app.Services, async ct =>
    {
        var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    });

    /// <summary>The module's set on a result line, the image's only set.</summary>
    private static MigrateSetResult Module(MigrateRunResult result) =>
        result.Sets.Should().ContainSingle(set => set.Name == AiModuleConstants.MigrationSetName).Subject;
}
