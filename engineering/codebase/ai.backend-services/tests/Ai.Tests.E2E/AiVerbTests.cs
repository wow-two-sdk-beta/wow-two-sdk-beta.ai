using System.Text.Json;
using Ai.Api.Configurations;
using Ai.Api.Operator;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Meta;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Ai.Tests.E2E;

/// <summary>The image's <c>ai</c> operator verbs — the eval bench Claude Code drives — over a migrated database: one JSON
/// line per verb, prompts and sets only in the tables, files as inputs, the matrix run and the combined report.</summary>
public sealed class AiVerbTests : IAsyncLifetime
{
    private const string Schema = """{"type":"object","required":["relevance"],"properties":{"relevance":{"type":"string","enum":["close","far","none"]}}}""";

    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    private readonly string folder = Path.Combine(Path.GetTempPath(), "ai-verbs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ServiceHost_ShouldDispatchTheAiVerbs_BeforeAnyHost()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await ServiceHost.RunAsync(["ai"], builder => builder.Configure(), app => app.ConfigureAsync(), verbs => verbs.AddAiVerbs(), output, error, CancellationToken.None);

        exit.Should().Be(AiOperatorWire.Refused);
        var line = Json(output.ToString().Trim());
        line.GetProperty("ok").GetBoolean().Should().BeFalse();
        line.GetProperty("error").GetString().Should().Be("usage");
        line.GetProperty("message").GetString().Should().Contain("ai eval run");
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("usecase", "put", "--key", "relevance")]
    [InlineData("usecase", "put", "--key", "relevance", "--mode", "rewrite", "--route", "anthropic/claude-haiku-5-5", "--unknown", "x")]
    [InlineData("usecase", "put", "--key", "relevance", "--mode", "sometimes", "--route", "anthropic/claude-haiku-5-5")]
    [InlineData("eval", "run", "--set", "x", "--model")]
    [InlineData("eval", "report", "--set", "x", "--format", "pdf")]
    [InlineData("eval", "rate", "--run", "not-a-guid", "--item", "x", "--rating", "5")]
    public async Task Verbs_ShouldRefuseBadArguments_WithAUsageLine(params string[] args)
    {
        var (exit, line) = await RunAsync(args);

        exit.Should().Be(AiOperatorWire.Refused);
        line.GetProperty("error").GetString().Should().Be("usage");
        line.GetProperty("message").GetString().Should().Contain("usage: ai");
    }

    [Fact]
    public async Task Verbs_ShouldFail_WithNoDatabase()
    {
        using var output = new StringWriter();

        var exit = await AiCommand.RunAsync(["usecase", "list"], new ConfigurationBuilder().Build(), output);

        exit.Should().Be(AiOperatorWire.Failed);
        Json(output.ToString().Trim()).GetProperty("error").GetString().Should().Be("no-database");
    }

    /// <summary>The bench against a provider with no key: every item of the run fails as not configured, and the report
    /// still renders in all three formats.</summary>
    [Fact]
    public async Task Bench_ShouldRunAndReport_EvenWhenTheProviderIsNotConfigured()
    {
        await SetUpAsync();
        await File.WriteAllTextAsync(Path.Combine(folder, "post-2.json"), """{"title":"Craft fair","views":12}""");
        Data(await RunAsync("eval", "add", "--set", "afisha-2", "--input-file", Path.Combine(folder, "post-2.json"),
            "--expected", """{"relevance":"far"}""", "--subject", "harvest:post:2@1", "--tags", "lang:en"));

        var (exit, line) = await RunAsync("eval", "run", "--set", "afisha-2", "--model", "anthropic/claude-haiku-5-5");

        exit.Should().Be(AiOperatorWire.Done, line.ToString());
        var summary = line.GetProperty("data").GetProperty("runs").EnumerateArray().Should().ContainSingle().Subject;
        summary.GetProperty("items").GetInt32().Should().Be(2);
        summary.GetProperty("failed").GetInt32().Should().Be(2);
        summary.GetProperty("accuracy").GetDouble().Should().Be(0);

        var report = Data(await RunAsync("eval", "report", "--set", "afisha-2", "--answers"));
        report.GetProperty("items").EnumerateArray().Should().HaveCount(2).And.OnlyContain(item => item.GetProperty("outcome").GetString() == "failed");
        var inferences = Data(await RunAsync("inferences", "--use-case", "relevance"));
        inferences.EnumerateArray().Should().HaveCount(2).And.OnlyContain(inference => inference.GetProperty("errorKind").GetString() == "notConfigured");

        var markdown = await RunTextAsync("eval", "report", "--set", "afisha-2", "--format", "md");
        markdown.Exit.Should().Be(AiOperatorWire.Done);
        markdown.Text.Should().Contain("| Run | Model | Prompt | Items | Accuracy |").And.Contain("anthropic/claude-haiku-5-5").And.Contain("failed");
        var csv = await RunTextAsync("eval", "report", "--set", "afisha-2", "--format", "csv");
        csv.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(3).And.Subject.First().Should().StartWith("run_id,provider,model");
    }

    /// <summary>The bench with scripted providers: a single run, the prompt list and labels, prices, the matrix across
    /// models and versions, a rating, and a set carried to another database by export and import.</summary>
    [Fact]
    public async Task Bench_ShouldDriveTheWholeLoop_WithScriptedProviders()
    {
        var caller = new ScriptedAiModelCaller
        {
            Respond = request => ScriptedAiModelCaller.Answer(
                request.User.Contains("Fashion", StringComparison.Ordinal) || request.Model.Model == "gpt-6-luna" ? """{"relevance":"close"}""" : """{"relevance":"none"}""",
                inputTokens: 1000, outputTokens: 100),
        };
        await SetUpAsync(mode: "versioned", caller: caller);

        var run = Data(await RunAsync(caller, "run", "--use-case", "relevance", "--var", "title=Tashkent Fashion Week", "--subject", "harvest:post:9@1"));
        run.GetProperty("outcome").GetString().Should().Be("succeeded");
        run.GetProperty("output").GetString().Should().Be("""{"relevance":"close"}""");
        run.GetProperty("costMicroUsd").GetInt64().Should().Be(150);

        await File.WriteAllTextAsync(Path.Combine(folder, "user-v2.txt"), "Rate this post again: {{title}}\n");
        var v2 = Data(await RunAsync(caller, "prompt", "save", "--use-case", "relevance", "--system-file", Path.Combine(folder, "system.txt"),
            "--user-file", Path.Combine(folder, "user-v2.txt"), "--max-tokens", "256", "--note", "shorter"));
        v2.GetProperty("version").GetInt32().Should().Be(2);
        v2.GetProperty("userTemplate").GetString().Should().Be("Rate this post again: {{title}}", "a file's final newline is dropped");
        Data(await RunAsync(caller, "label", "move", "--use-case", "relevance", "--label", "production", "--version", "1", "--by", "claude"))
            .GetProperty("version").GetInt32().Should().Be(1);
        var listed = Data(await RunAsync(caller, "prompt", "list", "--use-case", "relevance"));
        listed.GetProperty("prompts").EnumerateArray().Select(prompt => prompt.GetProperty("version").GetInt32()).Should().Equal(2, 1);
        listed.GetProperty("labels").EnumerateArray().Should().ContainSingle();

        Data(await RunAsync(caller, "price", "put", "--provider", "ollama", "--model", "llama3.2", "--input", "0", "--output", "0", "--effective-from", "2026-01-01"));
        Data(await RunAsync(caller, "price", "list")).EnumerateArray().Should().HaveCount(6);

        var (matrixExit, matrix) = await RunAsync(caller, "eval", "run", "--set", "afisha-2", "--model", "anthropic/claude-haiku-5-5,openai/gpt-6-luna",
            "--version", "1,2", "--parallel", "2", "--note", "matrix");
        matrixExit.Should().Be(AiOperatorWire.Done, matrix.ToString());
        var cells = matrix.GetProperty("data").GetProperty("runs").EnumerateArray().ToList();
        cells.Should().HaveCount(4);
        cells.Select(cell => (cell.GetProperty("run").GetProperty("model").GetString(), cell.GetProperty("run").GetProperty("promptVersion").GetInt32()))
            .Should().BeEquivalentTo([("claude-haiku-5-5", 1), ("claude-haiku-5-5", 2), ("gpt-6-luna", 1), ("gpt-6-luna", 2)]);
        cells.Single(cell => cell.GetProperty("run").GetProperty("model").GetString() == "gpt-6-luna" && cell.GetProperty("run").GetProperty("promptVersion").GetInt32() == 1)
            .GetProperty("accuracy").GetDouble().Should().Be(1);

        var report = Data(await RunAsync(caller, "eval", "report", "--set", "afisha-2", "--answers"));
        var item = report.GetProperty("items").EnumerateArray().First();
        var rated = Data(await RunAsync(caller, "eval", "rate", "--run", item.GetProperty("runId").GetString()!, "--item", item.GetProperty("itemId").GetString()!,
            "--rating", "4", "--comment", "fine", "--by", "claude"));
        rated.GetProperty("rating").GetInt32().Should().Be(4);
        Data(await RunAsync(caller, "totals", "--from", "2026-01-01", "--to", "2099-01-01")).EnumerateArray().Should().HaveCount(2);

        var file = Path.Combine(folder, "afisha-2.json");
        Data(await RunAsync(caller, "eval", "export", "--set", "afisha-2", "--out", file)).GetProperty("items").GetInt32().Should().Be(1);
        File.ReadAllText(file).Should().Contain("wow2.ai.eval-set.v1").And.Contain("Tashkent Fashion Week");
        await ResetDatabaseAsync();
        await SetUpAsync(mode: "versioned", caller: caller, withSet: false);
        var imported = Data(await RunAsync(caller, "eval", "import", "--file", file));
        imported.GetProperty("items").GetInt32().Should().Be(1);
        Data(await RunAsync(caller, "eval", "items", "--set", "afisha-2")).EnumerateArray().Single()
            .GetProperty("input").GetProperty("title").GetString().Should().Be("Tashkent Fashion Week");
    }

    [Fact]
    public async Task Run_ShouldRefuse_WhenNoModelAnswers_AndPrintTheAttempts()
    {
        await SetUpAsync();

        var (exit, line) = await RunAsync("run", "--use-case", "relevance", "--var", "title=x");

        exit.Should().Be(AiOperatorWire.Refused);
        line.GetProperty("error").GetString().Should().Be("failed");
        line.GetProperty("data").GetProperty("attempts").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("errorKind").GetString().Should().Be("notConfigured");
        (await RunAsync("run", "--use-case", "missing")).Line.GetProperty("error").GetString().Should().Be("not-found");
        (await RunAsync("run", "--use-case", "relevance")).Line.GetProperty("error").GetString().Should().Be("invalid", "the title variable is missing");
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(folder);
        await postgres.StartAsync();
        await MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await postgres.DisposeAsync();
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    /// <summary>Puts the relevance use case from a schema file, saves its prompt from files, creates the set and adds one
    /// item from an input file — as the developer does on the bench.</summary>
    private async Task SetUpAsync(string mode = "rewrite", ScriptedAiModelCaller? caller = null, bool withSet = true)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "schema.json"), Schema);
        await File.WriteAllTextAsync(Path.Combine(folder, "system.txt"), "You rate a post's relevance to fashion events.\n");
        await File.WriteAllTextAsync(Path.Combine(folder, "user.txt"), "Post: {{title}}\n");
        await File.WriteAllTextAsync(Path.Combine(folder, "post-1.json"), """{"title":"Tashkent Fashion Week"}""");

        var useCase = Data(await RunAsync(caller, "usecase", "put", "--key", "relevance", "--mode", mode, "--route", "anthropic/claude-haiku-5-5",
            "--schema-file", Path.Combine(folder, "schema.json"), "--description", "Relevance of a post", "--keep-outputs", "true"));
        useCase.GetProperty("promptMode").GetString().Should().Be(mode);
        useCase.GetProperty("outputSchema").GetString().Should().Contain("relevance");
        var prompt = Data(await RunAsync(caller, "prompt", "save", "--use-case", "relevance", "--system-file", Path.Combine(folder, "system.txt"),
            "--user-file", Path.Combine(folder, "user.txt"), "--temperature", "0", "--by", "max"));
        prompt.GetProperty("version").GetInt32().Should().Be(1);
        Data(await RunAsync(caller, "usecase", "list")).EnumerateArray().Should().ContainSingle();
        if (!withSet)
            return;

        Data(await RunAsync(caller, "eval", "set", "put", "--key", "afisha-2", "--use-case", "relevance", "--score-fields", "relevance",
            "--severe", "relevance:close>none", "--note", "bench"));
        var item = Data(await RunAsync(caller, "eval", "add", "--set", "afisha-2", "--input-file", Path.Combine(folder, "post-1.json"),
            "--expected", """{"relevance":"close"}""", "--subject", "harvest:post:1@1", "--tags", "lang:en,slice:positive", "--by", "max"));
        item.GetProperty("input").GetProperty("title").GetString().Should().Be("Tashkent Fashion Week");
        item.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Equal("lang:en", "slice:positive");
    }

    private Task<(int Exit, JsonElement Line)> RunAsync(params string[] args) => RunAsync(null, args);

    private async Task<(int Exit, JsonElement Line)> RunAsync(ScriptedAiModelCaller? caller, params string[] args)
    {
        var (exit, text) = await RunTextAsync(caller, args);
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle("each verb prints one JSON line");
        return (exit, Json(lines[0]));
    }

    private Task<(int Exit, string Text)> RunTextAsync(params string[] args) => RunTextAsync(null, args);

    private async Task<(int Exit, string Text)> RunTextAsync(ScriptedAiModelCaller? caller, params string[] args)
    {
        using var output = new StringWriter();
        var exit = await AiCommand.RunAsync(args, Configuration(), output,
            caller is null ? null : services => services.AddSingleton<IAiModelCaller>(caller));
        return (exit, output.ToString());
    }

    private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
        ["Ai:Module:RetryDelay"] = "00:00:00",
    }).Build();

    /// <summary>The deploy's migrate step, which runs before these verbs.</summary>
    private async Task MigrateAsync()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<IAiModelCaller>(new ScriptedAiModelCaller());
        services.AddAi(AiModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = postgres.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        (await provider.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    /// <summary>Drops the module's schema and migrates again: a fresh bench database.</summary>
    private async Task ResetDatabaseAsync()
    {
        await using (var connection = new Npgsql.NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var drop = new Npgsql.NpgsqlCommand($"DROP SCHEMA {AiModuleConstants.SchemaName} CASCADE", connection);
            await drop.ExecuteNonQueryAsync();
        }

        Npgsql.NpgsqlConnection.ClearAllPools();
        await MigrateAsync();
    }

    private static JsonElement Data((int Exit, JsonElement Line) result)
    {
        result.Exit.Should().Be(AiOperatorWire.Done, result.Line.ToString());
        result.Line.GetProperty("ok").GetBoolean().Should().BeTrue();
        return result.Line.GetProperty("data");
    }

    private static JsonElement Json(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}
