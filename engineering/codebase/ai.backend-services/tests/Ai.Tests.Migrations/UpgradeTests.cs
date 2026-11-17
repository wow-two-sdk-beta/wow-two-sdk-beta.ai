using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Migrations;

/// <summary>The queue's migrations as a release meets them: a database at <c>003</c> with eval history upgrades to the
/// queue, its runs reading as succeeded with their scored items, its indexes valid and its checks validated; an index left
/// invalid by an interrupted concurrent build is rebuilt when <c>005</c> runs again; a <c>005</c> database's calls and items
/// read as text-only after <c>006</c>.</summary>
/// <param name="fixture">The shared container, reset before each test.</param>
[Collection(MigratorCollection.Name)]
public sealed class UpgradeTests(MigratorPostgresFixture fixture) : IAsyncLifetime
{
    private const string Schema = AiModuleConstants.SchemaName;
    private const string Set = AiModuleConstants.MigrationSetName;

    private static readonly string[] QueueIndexes =
        ["ix_ai_eval_runs_queued", "ix_ai_eval_runs_running", "ix_ai_eval_runs_batch", "ix_ai_eval_runs_scope", "ix_ai_inferences_scope"];

    private static readonly Guid RunId = Guid.Parse("0192f0aa-0000-7000-8000-000000000001");
    private static readonly Guid FirstItem = Guid.Parse("0192f0aa-0000-7000-8000-0000000000a1");
    private static readonly Guid SecondItem = Guid.Parse("0192f0aa-0000-7000-8000-0000000000a2");

    /// <inheritdoc />
    public async Task InitializeAsync() => await fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Apply_ShouldUpgradeA003DatabaseWithEvalHistory_ToTheQueue()
    {
        await using var migrator = MigrationTests.CreateMigrator(fixture, options => options.AllowRollback = true);
        var runner = migrator.RunnerFor(Set);
        (await runner.ApplyPendingAsync("test")).ValueOrThrow();
        (await runner.RollbackAsync(targetOrdinal: 3)).ValueOrThrow().Should().Equal("006-image-inputs", "005-eval-run-queue-indexes", "004-eval-run-queue");
        await SeedHistoryAt003Async();

        var applied = (await runner.ApplyPendingAsync("release")).ValueOrThrow();

        applied.Should().Equal("004-eval-run-queue", "005-eval-run-queue-indexes", "006-image-inputs");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var run = await connection.QuerySingleAsync<(string Status, int Total, int Done, string Scope, Guid? Batch, Guid? Prompt)>(
            $"select status, items_total, items_done, scope, batch_id, prompt_id from {Schema}.ai_eval_runs where id = @RunId", new { RunId });
        run.Should().Be(("succeeded", 2, 2, "", null, null), "a run made before the queue finished synchronously and pinned nothing");
        (await connection.ExecuteScalarAsync<long>($"select count(*) from {Schema}.ai_inferences where scope = ''")).Should().Be(1);
        (await connection.ExecuteScalarAsync<string>($"select scope from {Schema}.ai_eval_sets where key = 'afisha-2'")).Should().BeEmpty();
        var expected = await connection.QueryAsync<string>(
            $"select expected::text from {Schema}.ai_eval_report_rows where run_id = @RunId order by item_id", new { RunId });
        expected.Should().Equal("""{"relevance": "close"}""", """{"relevance": "none"}""");
        await AssertQueueIndexesValidAsync(connection);
        (await connection.QueryAsync<(string Name, bool Validated)>(
            $"select conname, convalidated from pg_constraint where conrelid = '{Schema}.ai_eval_runs'::regclass and conname in " +
            "('ck_ai_eval_runs_status', 'ck_ai_eval_runs_progress', 'fk_ai_eval_runs_batch')"))
            .Should().HaveCount(3).And.OnlyContain(constraint => constraint.Validated, "005 validates 004's NOT VALID checks and key");

        // The module reads the old history as finished runs, reports them and lists them by status.
        var services = new ServiceCollection().AddLogging();
        services.AddAi().Configure(options => options.ConnectionString = fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var evals = scope.ServiceProvider.GetRequiredService<IEvalsBroker>();
        var model = (await evals.GetRunAsync(RunId)).ValueOrThrow();
        model.Status.Should().Be(EvalRunStatus.Succeeded);
        model.ItemsDone.Should().Be(2);
        model.PromptId.Should().BeNull();
        var report = (await evals.ReportAsync("afisha-2")).ValueOrThrow();
        report.Runs.Should().ContainSingle().Which.Correct.Should().Be(1);
        (await evals.ListRunsAsync(new EvalRunQuery { Status = EvalRunStatus.Succeeded })).ValueOrThrow().Should().ContainSingle();
    }

    [Fact]
    public async Task Apply_ShouldRebuildAnInvalidIndex_WhenAnInterruptedIndexBuildRunsAgain()
    {
        await using var migrator = MigrationTests.CreateMigrator(fixture);
        var runner = migrator.RunnerFor(Set);
        (await runner.ApplyPendingAsync("test")).ValueOrThrow();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);

        // What an interrupted CREATE INDEX CONCURRENTLY leaves: the index invalid, and 005 not recorded.
        await connection.ExecuteAsync($"update pg_index set indisvalid = false where indexrelid = '{Schema}.ix_ai_eval_runs_queued'::regclass");
        await connection.ExecuteAsync($"delete from {Schema}.{Set}_migration_history where ordinal = 5");

        var applied = (await runner.ApplyPendingAsync("retry")).ValueOrThrow();

        applied.Should().Equal("005-eval-run-queue-indexes");
        await AssertQueueIndexesValidAsync(connection);
    }

    [Fact]
    public async Task Apply_ShouldUpgradeA005Database_ItsCallsAndItemsReadingAsTextOnly()
    {
        await using var migrator = MigrationTests.CreateMigrator(fixture, options => options.AllowRollback = true);
        var runner = migrator.RunnerFor(Set);
        (await runner.ApplyPendingAsync("test")).ValueOrThrow();
        (await runner.RollbackAsync(targetOrdinal: 5)).ValueOrThrow().Should().Equal("006-image-inputs");
        await SeedHistoryAt003Async();

        (await runner.ApplyPendingAsync("release")).ValueOrThrow().Should().Equal("006-image-inputs");

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        (await connection.QuerySingleAsync<(int Count, string[] Hashes)>($"select image_count, image_hashes from {Schema}.ai_inferences"))
            .Should().Match<(int Count, string[] Hashes)>(row => row.Count == 0 && row.Hashes.Length == 0);
        (await connection.QueryAsync<string>($"select images::text from {Schema}.ai_eval_items")).Should().HaveCount(2).And.OnlyContain(images => images == "[]");

        var services = new ServiceCollection().AddLogging();
        services.AddAi().Configure(options => options.ConnectionString = fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IEvalsBroker>().GetItemAsync(FirstItem)).ValueOrThrow().Images.Should().BeEmpty();
        (await scope.ServiceProvider.GetRequiredService<IInferencesBroker>().ListAsync(new InferenceQuery { UseCase = "relevance" })).ValueOrThrow()
            .Should().ContainSingle().Which.ImageHashes.Should().BeEmpty();
    }

    private static async Task AssertQueueIndexesValidAsync(NpgsqlConnection connection)
    {
        var indexes = (await connection.QueryAsync<(string Name, bool Valid)>(
            "select c.relname, i.indisvalid from pg_index i join pg_class c on c.oid = i.indexrelid join pg_namespace n on n.oid = c.relnamespace " +
            $"where n.nspname = '{Schema}' and c.relname = any(@QueueIndexes)", new { QueueIndexes })).ToList();
        indexes.Select(index => index.Name).Should().BeEquivalentTo(QueueIndexes);
        indexes.Should().OnlyContain(index => index.Valid);
    }

    /// <summary>Writes a 003 database's eval history: a use case with a prompt, a set with two items, one finished run that
    /// scored both, and its inference.</summary>
    private async Task SeedHistoryAt003Async()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync($$"""
            insert into {{Schema}}.ai_use_cases (key, prompt_mode, route, keep_outputs, updated_at)
            values ('relevance', 'rewrite', '{anthropic/claude-haiku-5-5}', true, now());
            insert into {{Schema}}.ai_prompts (id, use_case, version, system_template, user_template, parameters, content_hash, created_at, updated_at)
            values (gen_random_uuid(), 'relevance', 1, 'system', 'user', '{}', 'aaaaaaaaaaaa', now(), now());
            insert into {{Schema}}.ai_eval_sets (key, use_case, score_fields, severe_misses, created_at)
            values ('afisha-2', 'relevance', '{relevance}', '{}', now());
            insert into {{Schema}}.ai_eval_items (id, set_key, input, expected, tags, created_at) values
                (@FirstItem, 'afisha-2', '{"title":"Fashion week"}', '{"relevance":"close"}', '{}', now()),
                (@SecondItem, 'afisha-2', '{"title":"Car fair"}', '{"relevance":"none"}', '{}', now());
            insert into {{Schema}}.ai_eval_runs (id, set_key, use_case, prompt_version, prompt_hash, provider, model, started_at, finished_at)
            values (@RunId, 'afisha-2', 'relevance', 1, 'aaaaaaaaaaaa', 'anthropic', 'claude-haiku-5-5', now(), now());
            insert into {{Schema}}.ai_inferences (id, at, use_case, prompt_version, prompt_hash, provider, model, attempt, input_hash, input_tokens,
                output_tokens, cache_read_tokens, cache_write_tokens, reasoning_tokens, cost_micro_usd, priced, latency_ms, outcome, eval_run_id)
            values (gen_random_uuid(), now(), 'relevance', 1, 'aaaaaaaaaaaa', 'anthropic', 'claude-haiku-5-5', 1, 'h', 100, 10, 0, 0, 0, 15, true, 40,
                'succeeded', @RunId);
            insert into {{Schema}}.ai_eval_scores (run_id, item_id, outcome, correct, severe, misses, cost_micro_usd, latency_ms, from_cache) values
                (@RunId, @FirstItem, 'succeeded', true, false, '{}', 15, 40, false),
                (@RunId, @SecondItem, 'succeeded', false, false, '{"relevance:none>close"}', 15, 40, false);
            """, new { RunId, FirstItem, SecondItem });
    }
}
