using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Inferences;

/// <summary>The inference log: filtered reads newest first, totals per use case and model over a period, and a subject's
/// purge.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class InferencesTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    [Fact]
    public async Task Totals_ShouldSumAttemptsTokensAndCost_PerUseCaseProviderAndModel()
    {
        var key = await UseCaseAsync();
        var start = Clock.GetUtcNow();
        Caller.Enqueue(
            ScriptedAiModelCaller.Answer(Json("close"), 1000, 200),
            ScriptedAiModelCaller.Error(AiErrorKind.BadRequest),
            ScriptedAiModelCaller.Answer(Json("far"), 2000, 400));
        await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a") });
        Clock.Advance(TimeSpan.FromHours(1));
        await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("b") });

        var totals = (await InferencesAsync(log => log.TotalsAsync(start, start.AddDays(1)))).ValueOrThrow();
        var later = (await InferencesAsync(log => log.TotalsAsync(start.AddMinutes(30), start.AddDays(1)))).ValueOrThrow();

        totals.Should().HaveCount(2);
        var haiku = totals.Single(total => total.Model == "claude-haiku-5-5");
        haiku.Attempts.Should().Be(2);
        haiku.Succeeded.Should().Be(1);
        haiku.InputTokens.Should().Be(1000);
        haiku.CostMicroUsd.Should().Be(200);
        totals.Single(total => total.Model == "gpt-6-luna").CostMicroUsd.Should().Be(400);
        later.Sum(total => total.Attempts).Should().Be(2, "the period is start-inclusive, and the first run is outside it");
        (await InferencesAsync(log => log.TotalsAsync(start, start))).Should().BeOfType<Result<IReadOnlyList<InferenceTotals>>.Failure>();
    }

    [Fact]
    public async Task List_ShouldFilterAndLimit_NewestFirst()
    {
        var key = await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        foreach (var subject in new[] { "s:1", "s:2", "s:2" })
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            await RunAsync(new AiRunRequest { UseCase = key, Variables = Post(subject + Clock.GetUtcNow().Second), Subject = subject });
        }

        var all = (await InferencesAsync(log => log.ListAsync(new InferenceQuery()))).ValueOrThrow();
        var two = (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Subject = "s:2", Limit = 1 }))).ValueOrThrow();

        all.Should().HaveCount(3);
        all.Select(inference => inference.AtUtc).Should().BeInDescendingOrder();
        two.Should().ContainSingle().Which.AtUtc.Should().Be(all[0].AtUtc);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Limit = 0 }))).Should().BeOfType<Result<IReadOnlyList<InferenceModel>>.Failure>();
    }

    [Fact]
    public async Task ForgetSubject_ShouldClearTheSubjectsOutputs_AndNothingElse()
    {
        var key = await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a"), Subject = "harvest:post:1@1" });
        await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("b"), Subject = "harvest:post:2@1" });

        var cleared = (await InferencesAsync(log => log.ForgetSubjectAsync("harvest:post:1@1"))).ValueOrThrow();
        var rerun = (await RunAsync(new AiRunRequest { UseCase = key, Variables = Post("a"), Subject = "harvest:post:1@1" })).ValueOrThrow();

        cleared.Should().Be(1);
        var rows = (await InferencesAsync(log => log.ListAsync(new InferenceQuery()))).ValueOrThrow();
        rows.Single(row => row.Subject == "harvest:post:2@1").Output.Should().NotBeNull();
        rerun.FromCache.Should().BeFalse("a forgotten output can no longer answer from the store");
    }
}
