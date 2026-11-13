using System.Diagnostics;
using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Runs;

/// <summary>The runner over PostgreSQL with scripted providers: rendering, fallback, retries, the schema check, the stored
/// answer cache, cost and the inference log.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class RunnerTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    [Fact]
    public async Task Run_ShouldAnswer_AndLogTheAttempt_WithItsPromptTokensAndCost()
    {
        var key = await UseCaseAsync();
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close"), inputTokens: 1000, outputTokens: 200, latencyMs: 420));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("Tashkent Fashion Week"), Subject = "harvest:post:1@1" })).ValueOrThrow();

        run.Outcome.Should().Be(AiOutcome.Succeeded);
        run.Output.Should().Be(Json("close"));
        run.FromCache.Should().BeFalse();
        var attempt = run.Attempts.Should().ContainSingle().Subject;
        attempt.Provider.Should().Be("anthropic");
        attempt.Model.Should().Be("claude-haiku-5-5");
        attempt.Attempt.Should().Be(1);
        attempt.PromptVersion.Should().Be(1);
        attempt.PromptHash.Should().MatchRegex("^[0-9a-f]{12}$");
        attempt.Subject.Should().Be("harvest:post:1@1");
        attempt.InputHash.Should().MatchRegex("^[0-9a-f]{64}$");
        attempt.Priced.Should().BeTrue();
        attempt.CostMicroUsd.Should().Be(200, "1000 input × 0.1 + 200 output × 0.5 micro-USD at the seeded Haiku price");
        attempt.LatencyMs.Should().Be(420);
        attempt.AtUtc.Should().Be(Clock.GetUtcNow());
        attempt.Output.Should().Be(Json("close"));

        var request = Caller.Requests.Should().ContainSingle().Subject;
        request.User.Should().Be("Post: Tashkent Fashion Week");
        request.JsonSchema.Should().Contain("\"relevance\"");
        request.SchemaName.Should().Be(key);
        request.MaxOutputTokens.Should().Be(1024, "the module's default applies when the prompt names none");

        var logged = (await InferencesAsync(log => log.ListAsync(new InferenceQuery { UseCase = key }))).ValueOrThrow();
        logged.Should().ContainSingle().Which.Should().BeEquivalentTo(attempt);
    }

    [Fact]
    public async Task Run_ShouldFallBack_OnFailureRefusalAndSchemaInvalidAnswers()
    {
        var key = await UseCaseAsync(route: [Haiku, Luna, "anthropic/claude-sonnet-5-5", "anthropic/claude-opus-5-5"]);
        Caller.Enqueue(
            ScriptedAiModelCaller.Error(AiErrorKind.BadRequest, "bad"),
            ScriptedAiModelCaller.Refusal(),
            ScriptedAiModelCaller.Answer("the post is close to fashion"),
            ScriptedAiModelCaller.Answer("```json\n" + Json("far") + "\n```"));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        run.Outcome.Should().Be(AiOutcome.Succeeded);
        run.Output.Should().Be(Json("far"), "the fence is stripped");
        run.Attempts.Select(attempt => (attempt.Model, attempt.Outcome, attempt.ErrorKind)).Should().Equal(
            ("claude-haiku-5-5", AiOutcome.Failed, AiErrorKind.BadRequest),
            ("gpt-6-luna", AiOutcome.Refused, AiErrorKind.ContentFiltered),
            ("claude-sonnet-5-5", AiOutcome.SchemaInvalid, AiErrorKind.SchemaMismatch),
            ("claude-opus-5-5", AiOutcome.Succeeded, (AiErrorKind?)null));
        run.Attempts.Select(attempt => attempt.Attempt).Should().Equal(1, 2, 3, 4);
        run.Attempts[2].Output.Should().Be("the post is close to fashion", "a schema-invalid answer is kept for review");
        run.CostMicroUsd.Should().Be(run.Attempts.Sum(attempt => attempt.CostMicroUsd));
    }

    [Fact]
    public async Task Run_ShouldEndWithTheLastOutcome_WhenEveryModelFails()
    {
        var key = await UseCaseAsync();
        Caller.Enqueue(ScriptedAiModelCaller.Error(AiErrorKind.Unauthorized), ScriptedAiModelCaller.Refusal());

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        run.Outcome.Should().Be(AiOutcome.Refused);
        run.Output.Should().BeNull();
        run.Attempts.Should().HaveCount(2, "a non-transient failure moves to the next model without a retry");
    }

    [Fact]
    public async Task Run_ShouldRetryTransientFailures_ThenFallBack_WhenTheRetriesRunOut()
    {
        var key = await UseCaseAsync();
        Caller.Enqueue(
            ScriptedAiModelCaller.Error(AiErrorKind.RateLimited),
            ScriptedAiModelCaller.Error(AiErrorKind.Unavailable),
            ScriptedAiModelCaller.Error(AiErrorKind.Timeout),
            ScriptedAiModelCaller.Error(AiErrorKind.Network),
            ScriptedAiModelCaller.Answer(Json("close")));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        run.Outcome.Should().Be(AiOutcome.Succeeded);
        run.Attempts.Select(attempt => attempt.Model).Should().Equal(
            "claude-haiku-5-5", "claude-haiku-5-5", "claude-haiku-5-5", "gpt-6-luna", "gpt-6-luna");
    }

    [Fact]
    public async Task Run_ShouldServeAStoredAnswer_ForTheSamePromptModelAndInput_UnlessFresh()
    {
        var key = await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var first = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();
        var cached = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();
        var other = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("b") })).ValueOrThrow();
        var fresh = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a"), Fresh = true })).ValueOrThrow();

        cached.FromCache.Should().BeTrue();
        cached.Output.Should().Be(first.Output);
        cached.Attempts.Should().ContainSingle().Which.Id.Should().Be(first.Final.Id, "a cache hit reuses the stored inference");
        cached.CostMicroUsd.Should().Be(first.CostMicroUsd);
        other.FromCache.Should().BeFalse();
        fresh.FromCache.Should().BeFalse();
        Caller.Requests.Should().HaveCount(3);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { UseCase = key }))).ValueOrThrow().Should().HaveCount(3);
    }

    [Fact]
    public async Task Run_ShouldMissTheCache_WhenARewriteChangesThePromptText()
    {
        var key = await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        var before = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        await SaveAsync(key, "Rewritten: {{title}}");
        var after = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        after.FromCache.Should().BeFalse();
        after.Final.PromptVersion.Should().Be(before.Final.PromptVersion, "a rewrite keeps the version");
        after.Final.PromptHash.Should().NotBe(before.Final.PromptHash, "the content hash tells the two texts apart");
    }

    [Fact]
    public async Task Run_ShouldKeepNoOutput_AndServeNoCache_WhenTheUseCaseKeepsNoOutputs()
    {
        var key = await UseCaseAsync(keepOutputs: false);
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var first = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();
        var second = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        first.Output.Should().Be(Json("close"), "the caller still gets its answer");
        first.Final.Output.Should().BeNull();
        second.FromCache.Should().BeFalse();
        Caller.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Run_ShouldPriceNothing_ForAModelWithNoPriceRow()
    {
        var key = await UseCaseAsync(route: ["ollama/llama3.2"]);
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close"), inputTokens: 5000, outputTokens: 500));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        run.Final.Priced.Should().BeFalse();
        run.Final.CostMicroUsd.Should().Be(0);
        run.Final.InputTokens.Should().Be(5000);
    }

    [Fact]
    public async Task Run_ShouldPriceCacheReadsAndWrites_AtTheirOwnPrices()
    {
        var key = await UseCaseAsync();
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close"), inputTokens: 10_000, outputTokens: 2_000, cacheReadTokens: 3_000, cacheWriteTokens: 1_000));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        run.Final.CostMicroUsd.Should().Be(600 + 30 + 125 + 1000);
        run.Final.CacheReadTokens.Should().Be(3_000);
        run.Final.CacheWriteTokens.Should().Be(1_000);
    }

    [Fact]
    public async Task Run_ShouldRefuseBeforeAnyCall_WhenAVariableIsMissing()
    {
        var key = await UseCaseAsync(user: "{{title}} in {{language}}");

        var result = await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") });

        var error = result.Should().BeOfType<Result<AiRunModel>.Failure>().Subject.Error;
        error.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().Contain("language");
        Caller.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ShouldRefuse_AnUnknownUseCaseAnEmptyRouteOrNoPrompt()
    {
        var empty = await UseCaseAsync(key: "empty-route", route: []);
        await PromptsAsync(prompts => prompts.PutUseCaseAsync(new UseCaseModel { Key = "no-prompt", Route = [Haiku] }));

        Failure(await RunAsync(new AiRunRequest { UseCase = "missing" })).Type.Should().Be(AppErrorType.NotFound);
        Failure(await RunAsync(new AiRunRequest { UseCase = empty, Variables = Post("a") })).Message.Should().Contain("no model route");
        Failure(await RunAsync(new AiRunRequest { UseCase = "no-prompt" })).Message.Should().Contain("has no prompt");
        Failure(await RunAsync(new AiRunRequest { UseCase = empty, Variables = Post("a"), Models = ["not-a-ref"] })).Type.Should().Be(AppErrorType.Validation);
        Caller.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ShouldSkipRoutedModelsWithNoPrompt_AndUseEachModelsOwnRow()
    {
        await PromptsAsync(prompts => prompts.PutUseCaseAsync(new UseCaseModel { Key = "per-model", Route = [Luna, Haiku, "anthropic/claude-sonnet-5-5"] }));
        await SaveAsync("per-model", "haiku: {{title}}", match: "claude-haiku-*", parameters: new PromptParameters { MaxOutputTokens = 64, Temperature = 0 });
        await SaveAsync("per-model", "claude: {{title}}", match: "claude-*");
        Caller.Enqueue(ScriptedAiModelCaller.Error(AiErrorKind.InvalidResponse), ScriptedAiModelCaller.Answer("ok"));

        var run = (await RunAsync(new AiRunRequest { UseCase = "per-model", Variables = Post("a") })).ValueOrThrow();

        run.Attempts.Select(attempt => attempt.Model).Should().Equal("claude-haiku-5-5", "claude-sonnet-5-5");
        run.Attempts[0].PromptModelMatch.Should().Be("claude-haiku-*");
        Caller.Requests.Select(request => request.User).Should().Equal("haiku: a", "claude: a");
        Caller.Requests[0].MaxOutputTokens.Should().Be(64);
        Caller.Requests[0].Temperature.Should().Be(0);
        run.Output.Should().Be("ok", "a use case with no schema takes free text");
    }

    [Fact]
    public async Task Run_ShouldTakeTheRequestsModels_InPlaceOfTheRoute()
    {
        var key = await UseCaseAsync();
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("none")));

        var run = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a"), Models = ["openai/gpt-6.1-terra"] })).ValueOrThrow();

        run.Final.Model.Should().Be("gpt-6.1-terra");
        run.Final.Priced.Should().BeTrue();
    }

    [Fact]
    public async Task Run_ShouldTagEachAttemptsActivity_WithTheGenAiAttributes()
    {
        var key = await UseCaseAsync(key: "telemetry-case");
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AiModuleTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("gen_ai.prompt.name"), "telemetry-case"))
                    lock (activities)
                        activities.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(listener);
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close"), inputTokens: 321, outputTokens: 12));

        await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") });

        var activity = activities.Should().ContainSingle().Subject;
        activity.GetTagItem("gen_ai.request.model").Should().Be("claude-haiku-5-5");
        activity.GetTagItem("gen_ai.usage.input_tokens").Should().Be(321L);
        activity.GetTagItem("gen_ai.usage.output_tokens").Should().Be(12L);
        activity.GetTagItem("gen_ai.prompt.version").Should().Be(1);
    }

    [Fact]
    public async Task Run_ShouldServeTheProductionLabelsVersion_ForAVersionedUseCase()
    {
        var key = await UseCaseAsync(mode: PromptMode.Versioned, user: "v1 {{title}}");
        await SaveAsync(key, "v2 {{title}}");
        await PromptsAsync(prompts => prompts.MoveLabelAsync(key, AiConstants.ProductionLabel, 1));
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var production = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();
        var explicitVersion = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a"), Version = 2 })).ValueOrThrow();
        await PromptsAsync(prompts => prompts.MoveLabelAsync(key, AiConstants.ProductionLabel, 2));
        var moved = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") })).ValueOrThrow();

        production.Final.PromptVersion.Should().Be(1);
        explicitVersion.Final.PromptVersion.Should().Be(2);
        moved.Final.PromptVersion.Should().Be(2);
        moved.FromCache.Should().BeTrue("version 2 answered the same input before");
        Caller.Requests.Select(request => request.User).Should().Equal("v1 a", "v2 a");
    }

    private static AppError Failure(Result<AiRunModel> result) => result.Should().BeOfType<Result<AiRunModel>.Failure>().Subject.Error;
}

/// <summary>The runner's backoff waits on the module's clock: a retry starts only after the clock moved by the delay.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class RunnerBackoffTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    /// <inheritdoc />
    protected override void Configure(AiModuleOptions options)
    {
        options.RetryDelay = TimeSpan.FromSeconds(1);
        options.RetriesPerModel = 2;
    }

    [Fact]
    public async Task Run_ShouldWaitTheDoublingDelay_BeforeEachRetry()
    {
        var key = await UseCaseAsync(route: [Haiku]);
        var started = new List<DateTimeOffset>();
        Caller.Respond = _ =>
        {
            lock (started)
                started.Add(Clock.GetUtcNow());
            return started.Count < 3 ? ScriptedAiModelCaller.Error(AiErrorKind.RateLimited) : ScriptedAiModelCaller.Answer(Json("close"));
        };

        var pending = RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") });
        for (var tick = 0; tick < 200 && !pending.IsCompleted; tick++)
        {
            await Task.Delay(20);
            Clock.Advance(TimeSpan.FromMilliseconds(250));
        }

        var run = (await pending).ValueOrThrow();
        run.Outcome.Should().Be(AiOutcome.Succeeded);
        started.Should().HaveCount(3);
        (started[1] - started[0]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
        (started[2] - started[1]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2));
    }
}
