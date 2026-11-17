using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Scopes;

/// <summary>Data scopes in one database: two tenants' calls, sets and runs over one shared use case and prompt, each kept
/// apart by the filters, the cache, the purge and the set's scope.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class ScopeTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    private const string TenantA = "tenant:a";
    private const string TenantB = "tenant:b";

    [Fact]
    public async Task Runs_ShouldKeepEachScope_InTheLogTotalsAndCache()
    {
        await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"), 1000, 100);

        var a = (await RunAsync(new AiRunRequest { UseCase = "relevance", Variables = Post("same post"), Subject = "post:1", Scope = TenantA })).ValueOrThrow();
        var b = (await RunAsync(new AiRunRequest { UseCase = "relevance", Variables = Post("same post"), Subject = "post:1", Scope = TenantB })).ValueOrThrow();
        var bAgain = (await RunAsync(new AiRunRequest { UseCase = "relevance", Variables = Post("same post"), Subject = "post:1", Scope = TenantB })).ValueOrThrow();
        var shared = (await RunAsync(new AiRunRequest { UseCase = "relevance", Variables = Post("other post") })).ValueOrThrow();

        a.Final.Scope.Should().Be(TenantA);
        b.FromCache.Should().BeFalse("a stored answer of another scope is never reused");
        bAgain.FromCache.Should().BeTrue();
        bAgain.Final.Id.Should().Be(b.Final.Id);
        shared.Final.Scope.Should().BeNull("a call naming no scope lands in the unscoped bucket");
        Caller.Requests.Should().HaveCount(3);

        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Scopes = [TenantA] }))).ValueOrThrow()
            .Should().ContainSingle().Which.Id.Should().Be(a.Final.Id);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Scopes = [AiConstants.Unscoped] }))).ValueOrThrow()
            .Should().ContainSingle().Which.Id.Should().Be(shared.Final.Id);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Scopes = [TenantA, TenantB] }))).ValueOrThrow().Should().HaveCount(2);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Scopes = [] }))).ValueOrThrow().Should().BeEmpty();
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery()))).ValueOrThrow().Should().HaveCount(3);

        var period = (From: Clock.GetUtcNow().AddDays(-1), To: Clock.GetUtcNow().AddDays(1));
        var totalsA = (await InferencesAsync(log => log.TotalsAsync(new InferenceTotalsQuery { FromUtc = period.From, ToUtc = period.To, Scopes = [TenantA] }))).ValueOrThrow();
        totalsA.Should().ContainSingle().Which.Attempts.Should().Be(1);
        (await InferencesAsync(log => log.TotalsAsync(period.From, period.To))).ValueOrThrow().Single().Attempts.Should().Be(3);

        (await InferencesAsync(log => log.ForgetSubjectAsync("post:1", [TenantA]))).ValueOrThrow().Should().Be(1, "a tenant's purge stays in its scope");
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { Scopes = [TenantB] }))).ValueOrThrow().Should().OnlyContain(inference => inference.Output != null);
    }

    [Fact]
    public async Task Sets_ShouldKeepTheirScope_ForTheirRuns_AndRefuseAMove()
    {
        await UseCaseAsync();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        foreach (var (key, scope) in new[] { ("set-a", TenantA), ("set-b", TenantB) })
        {
            (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = key, UseCase = "relevance", Scope = scope }))).ValueOrThrow().Scope.Should().Be(scope);
            (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = key, Input = Post(key), Expected = Json("close") }))).ValueOrThrow();
        }

        var runA = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = "set-a", Model = Haiku }))).ValueOrThrow();
        var batchB = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = "set-b", Models = [Haiku] }))).ValueOrThrow();

        runA.Scope.Should().Be(TenantA);
        batchB.Scope.Should().Be(TenantB);
        batchB.Runs.Should().OnlyContain(run => run.Scope == TenantB);
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { EvalRunId = runA.Id }))).ValueOrThrow().Should().OnlyContain(inference => inference.Scope == TenantA);
        (await EvalsAsync(evals => evals.ListRunsAsync(new EvalRunQuery { Scopes = [TenantB] }))).ValueOrThrow()
            .Should().ContainSingle().Which.Id.Should().Be(batchB.Runs[0].Id);
        (await EvalsAsync(evals => evals.ListRunsAsync(new EvalRunQuery { Scopes = [AiConstants.Unscoped] }))).ValueOrThrow().Should().BeEmpty();

        var moved = await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = "set-a", UseCase = "relevance", Scope = TenantB }));
        moved.Should().BeOfType<Result<EvalSetModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Conflict);
        (await EvalsAsync(evals => evals.GetSetAsync("set-a"))).ValueOrThrow().Scope.Should().Be(TenantA);
        var tooLong = await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = "set-c", UseCase = "relevance", Scope = new string('x', 129) }));
        tooLong.Should().BeOfType<Result<EvalSetModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }
}
