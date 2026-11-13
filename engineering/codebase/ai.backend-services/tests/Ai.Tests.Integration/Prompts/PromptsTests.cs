using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Prompts;

/// <summary>The prompt registry in the module's own tables: both modes share <c>ai_prompts</c>, and every label move is
/// kept in <c>ai_prompt_label_moves</c>.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class PromptsTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    [Fact]
    public async Task AddAi_ShouldApplyItsSet_InTheModulesOwnSchema()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);

        var tables = (await connection.QueryAsync<string>(
            "select table_schema || '.' || table_name from information_schema.tables where table_schema in ('public', @schema)",
            new { schema = AiModuleConstants.SchemaName })).ToList();

        tables.Should().Contain($"{AiModuleConstants.SchemaName}.ai_prompts")
            .And.Contain($"{AiModuleConstants.SchemaName}.ai_eval_report_rows")
            .And.NotContain(table => table.StartsWith("public.", StringComparison.Ordinal), "the module writes nothing into the consumer's schema");
    }

    [Fact]
    public async Task BothModes_ShouldShareOneTable_OneRowPerRewriteAndOnePerVersion()
    {
        await UseCaseAsync(key: "rewritten", mode: PromptMode.Rewrite);
        await UseCaseAsync(key: "versioned", mode: PromptMode.Versioned);
        for (var save = 0; save < 3; save++)
        {
            await SaveAsync("rewritten", $"text {save}");
            await SaveAsync("versioned", $"text {save}");
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        var rows = (await connection.QueryAsync<(string UseCase, int Version, string UserTemplate)>(
            $"select use_case, version, user_template from {AiModuleConstants.SchemaName}.ai_prompts order by use_case, version")).ToList();

        rows.Should().Equal(
            ("rewritten", 1, "text 2"),
            ("versioned", 1, "Post: {{title}}"),
            ("versioned", 2, "text 0"),
            ("versioned", 3, "text 1"),
            ("versioned", 4, "text 2"));
    }

    [Fact]
    public async Task MoveLabel_ShouldKeepEveryMove_InTheHistory()
    {
        await UseCaseAsync(key: "versioned", mode: PromptMode.Versioned);
        await SaveAsync("versioned", "v2");

        await PromptsAsync(prompts => prompts.MoveLabelAsync("versioned", AiConstants.ProductionLabel, 1, "max"));
        Clock.Advance(TimeSpan.FromMinutes(1));
        await PromptsAsync(prompts => prompts.MoveLabelAsync("versioned", AiConstants.ProductionLabel, 2, "claude"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        var moves = (await connection.QueryAsync<(int? FromVersion, int Version, string MovedBy)>(
            $"select from_version, version, moved_by from {AiModuleConstants.SchemaName}.ai_prompt_label_moves order by moved_at")).ToList();
        moves.Should().Equal((null, 1, "max"), (1, 2, "claude"));
        (await PromptsAsync(prompts => prompts.ListLabelsAsync("versioned"))).ValueOrThrow()
            .Should().ContainSingle().Which.Should().Match<PromptLabelModel>(label => label.Version == 2 && label.MovedBy == "claude" && label.MovedAtUtc == Clock.GetUtcNow());
    }

    [Fact]
    public async Task Prices_ShouldListTheSeededRows_AndKeepHistory()
    {
        var put = (await PricesAsync(prices => prices.PutAsync(new WoW.Two.Sdk.Beta.Be.Ai.Prices.ModelPriceModel
        {
            Provider = "Anthropic",
            Model = "claude-haiku-5-5",
            EffectiveFromUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            InputPerMTokMicroUsd = 80_000,
            OutputPerMTokMicroUsd = 400_000,
            Source = "test",
        }))).ValueOrThrow();

        var all = (await PricesAsync(prices => prices.ListAsync())).ValueOrThrow();

        put.Provider.Should().Be("anthropic");
        all.Should().HaveCount(6, "five seeded rows and the new one");
        all.Where(price => price.Model == "claude-haiku-5-5").Select(price => price.InputPerMTokMicroUsd).Should().Equal(80_000, 100_000);
        all.Single(price => price.Model == "gpt-6.1-terra").Should().Match<WoW.Two.Sdk.Beta.Be.Ai.Prices.ModelPriceModel>(price =>
            price.OutputPerMTokMicroUsd == 5_000_000 && price.CacheReadPerMTokMicroUsd == 50_000 && price.Source == "https://developers.openai.com/api/docs/pricing");
    }
}
