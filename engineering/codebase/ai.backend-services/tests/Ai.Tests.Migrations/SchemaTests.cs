using AwesomeAssertions;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Migrations;

/// <summary>What the module's set builds: every table of the AI layer, the report view, and the seeded prices.</summary>
/// <param name="fixture">The shared container, reset before each test.</param>
[Collection(MigratorCollection.Name)]
public sealed class SchemaTests(MigratorPostgresFixture fixture) : IAsyncLifetime
{
    private const string Schema = AiModuleConstants.SchemaName;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await using var migrator = MigrationTests.CreateMigrator(fixture);
        (await migrator.RunnerFor(AiModuleConstants.MigrationSetName).ApplyPendingAsync("test")).ValueOrThrow();
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("ai_use_cases")]
    [InlineData("ai_prompts")]
    [InlineData("ai_prompt_labels")]
    [InlineData("ai_prompt_label_moves")]
    [InlineData("ai_model_prices")]
    [InlineData("ai_inferences")]
    [InlineData("ai_eval_sets")]
    [InlineData("ai_eval_items")]
    [InlineData("ai_eval_runs")]
    [InlineData("ai_eval_scores")]
    public async Task Apply_ShouldCreateEveryTable_InTheModulesSchema(string table)
    {
        await using var migrator = MigrationTests.CreateMigrator(fixture);

        (await migrator.HasTableAsync(Schema, table)).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_ShouldSeedTheNovemberPrices_AndCreateTheReportView()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);

        var prices = (await connection.QueryAsync<(string Provider, string Model, long Input, long Output, long CacheWrite, long CacheRead)>(
            $"select provider, model, input_per_mtok_micro_usd, output_per_mtok_micro_usd, cache_write_per_mtok_micro_usd, cache_read_per_mtok_micro_usd " +
            $"from {Schema}.ai_model_prices where effective_from = '2026-11-10T00:00:00Z' order by provider, model")).ToList();
        var view = await connection.ExecuteScalarAsync<long>($"select count(*) from {Schema}.ai_eval_report_rows");

        prices.Should().Equal(
            ("anthropic", "claude-haiku-5-5", 100_000L, 500_000L, 125_000L, 10_000L),
            ("anthropic", "claude-opus-5-5", 4_000_000L, 20_000_000L, 5_000_000L, 200_000L),
            ("anthropic", "claude-sonnet-5-5", 2_000_000L, 10_000_000L, 2_500_000L, 100_000L),
            ("openai", "gpt-6-luna", 100_000L, 500_000L, 125_000L, 10_000L),
            ("openai", "gpt-6.1-terra", 1_000_000L, 5_000_000L, 1_250_000L, 50_000L));
        view.Should().Be(0);
    }

    [Fact]
    public async Task Prompts_ShouldRefuseASecondRow_ForTheSameUseCaseMatchAndVersion()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync($"insert into {Schema}.ai_use_cases (key, prompt_mode, route, keep_outputs, updated_at) values ('k', 'rewrite', '{{}}', true, now())");
        const string Insert = "insert into {0}.ai_prompts (id, use_case, model_match, version, system_template, user_template, parameters, content_hash, created_at, updated_at) " +
            "values (gen_random_uuid(), 'k', null, 1, 's', 'u', '{{}}', '000000000000', now(), now())";
        await connection.ExecuteAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, Insert, Schema));

        var second = () => connection.ExecuteAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, Insert, Schema));

        (await second.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
