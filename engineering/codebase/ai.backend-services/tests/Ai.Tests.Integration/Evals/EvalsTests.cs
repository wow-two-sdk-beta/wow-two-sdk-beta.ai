using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Evals;

/// <summary>Evals over PostgreSQL with scripted providers: sets and items, runs scored against expected answers, the
/// combined report's numbers, ratings, removal, subject sources and export/import.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class EvalsTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    private const string Set = "afisha-4";

    /// <inheritdoc />
    protected override void AddServices(IServiceCollection collection) => collection.AddSingleton<IEvalSubjectSource>(new PostSource());

    [Fact]
    public async Task Run_ShouldScoreEveryItem_AndReportAccuracyMissesCostAndLatency()
    {
        await SetAsync();
        var run = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Note = "baseline" }))).ValueOrThrow();

        run.PromptVersion.Should().Be(1);
        run.PromptHash.Should().MatchRegex("^[0-9a-f]{12}$");
        run.Provider.Should().Be("anthropic");
        run.FinishedAtUtc.Should().NotBeNull();
        Caller.Requests.Should().HaveCount(4).And.OnlyContain(request => request.Model.Model == "claude-haiku-5-5", "an eval run never falls back");

        var report = (await EvalsAsync(evals => evals.ReportAsync(Set, withAnswers: true))).ValueOrThrow();
        report.UseCase.Should().Be("relevance");
        var summary = report.Runs.Should().ContainSingle().Subject;
        summary.Run.Should().BeEquivalentTo(run);
        summary.Items.Should().Be(4);
        summary.Answered.Should().Be(2);
        summary.Correct.Should().Be(1);
        summary.Accuracy.Should().Be(0.25);
        summary.SevereMisses.Should().Be(1);
        summary.SchemaInvalid.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.FromCache.Should().Be(0);
        summary.CostMicroUsd.Should().Be(3 * 200, "three answers cost 200 micro-USD each; the refusal used no tokens");
        summary.CostPerItemMicroUsd.Should().Be(150);
        summary.InputTokens.Should().Be(3000);
        summary.OutputTokens.Should().Be(600);
        summary.P50LatencyMs.Should().Be(100);
        summary.P95LatencyMs.Should().Be(300);
        summary.AccuracyByTag.Should().Contain("lang:ru", 0.5).And.Contain("lang:en", 0.0);

        var bySubject = report.Items.ToDictionary(item => item.Subject!);
        bySubject["post:close"].Correct.Should().BeTrue();
        bySubject["post:close"].Answer.Should().Be("""{"relevance":"close"}""");
        bySubject["post:missed"].Misses.Should().Equal("relevance:close>none");
        bySubject["post:missed"].Severe.Should().BeTrue();
        bySubject["post:broken"].Outcome.Should().Be(AiOutcome.SchemaInvalid);
        bySubject["post:refused"].Outcome.Should().Be(AiOutcome.Refused);
        bySubject["post:refused"].InferenceId.Should().NotBeNull();

        var logged = (await InferencesAsync(log => log.ListAsync(new InferenceQuery { EvalRunId = run.Id }))).ValueOrThrow();
        logged.Should().HaveCount(4).And.OnlyContain(inference => inference.EvalRunId == run.Id);
        logged.Select(inference => inference.Subject).Should().BeEquivalentTo("post:close", "post:missed", "post:broken", "post:refused");
    }

    [Fact]
    public async Task Run_ShouldReuseStoredAnswers_OnARerun_UnlessFresh()
    {
        await SetAsync();
        var first = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();
        var second = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Parallelism = 3 }))).ValueOrThrow();
        var fresh = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Fresh = true }))).ValueOrThrow();

        var report = (await EvalsAsync(evals => evals.ReportAsync(Set, [first.Id, second.Id, fresh.Id]))).ValueOrThrow();
        var runs = report.Runs.ToDictionary(summary => summary.Run.Id);
        runs[second.Id].FromCache.Should().Be(2, "the two schema-valid answers are stored");
        runs[second.Id].Correct.Should().Be(runs[first.Id].Correct);
        runs[second.Id].CostMicroUsd.Should().Be(runs[first.Id].CostMicroUsd, "a cached answer keeps its original cost");
        runs[fresh.Id].FromCache.Should().Be(0);
        Caller.Requests.Should().HaveCount(4 + 2 + 4, "a rerun calls the model only for items with no stored answer");
        report.Items.Should().HaveCount(12);
        report.Items.Should().OnlyContain(item => item.Answer == null, "answers only come on request");
    }

    [Fact]
    public async Task Run_ShouldRunEachPromptVersion_OfAVersionedUseCase()
    {
        await SetAsync(PromptMode.Versioned);
        await SaveAsync("relevance", "v2 Post: {{title}}");

        var v1 = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Version = 1 }))).ValueOrThrow();
        Clock.Advance(TimeSpan.FromMinutes(1));
        var v2 = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Luna }))).ValueOrThrow();
        var missing = await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Label = AiConstants.CandidateLabel }));

        v1.PromptVersion.Should().Be(1);
        v2.PromptVersion.Should().Be(2, "with no label the newest version runs");
        v2.PromptHash.Should().NotBe(v1.PromptHash);
        Caller.Requests.Count(request => request.User.StartsWith("v2 ", StringComparison.Ordinal)).Should().Be(4);
        missing.Should().BeOfType<Result<EvalRunModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.NotFound);
        (await EvalsAsync(evals => evals.ListRunsAsync(Set))).ValueOrThrow().Select(run => run.Id).Should().Equal(v1.Id, v2.Id);
    }

    [Fact]
    public async Task Rate_ShouldShowInTheReport_AndReplaceAnEarlierRating()
    {
        await SetAsync();
        var run = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();
        var items = (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow();

        await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = items[0].Id, Rating = 2, By = "claude" }));
        var rated = (await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = items[0].Id, Rating = 4, Comment = "close enough" }))).ValueOrThrow();
        await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = items[1].Id, Rating = 5 }));

        rated.Rating.Should().Be(4);
        rated.Comment.Should().Be("close enough");
        var report = (await EvalsAsync(evals => evals.ReportAsync(Set))).ValueOrThrow();
        report.Runs.Single().MeanRating.Should().Be(4.5);
        Error(await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = items[0].Id, Rating = 6 }))).Type.Should().Be(AppErrorType.Validation);
        Error(await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = Guid.NewGuid(), ItemId = items[0].Id, Rating = 3 }))).Type.Should().Be(AppErrorType.NotFound);
        Error(await EvalsAsync(evals => evals.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = Guid.NewGuid(), Rating = 3 }))).Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task RemoveAndRelabel_ShouldKeepPastRuns_AndChangeOnlyLaterOnes()
    {
        await SetAsync();
        var before = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();
        var items = (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow();
        var missed = items.Single(item => item.Subject == "post:missed");
        var refused = items.Single(item => item.Subject == "post:refused");

        (await EvalsAsync(evals => evals.RemoveItemAsync(refused.Id))).ValueOrThrow().Should().BeTrue();
        (await EvalsAsync(evals => evals.RemoveItemAsync(refused.Id))).ValueOrThrow().Should().BeFalse("it is gone already");
        var relabelled = (await EvalsAsync(evals => evals.RelabelItemAsync(missed.Id, """{"relevance":"none"}""", "max"))).ValueOrThrow();
        var after = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();

        relabelled.Expected.Should().Be("""{"relevance":"none"}""");
        relabelled.LabelledBy.Should().Be("max");
        (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow().Should().HaveCount(3);
        var report = (await EvalsAsync(evals => evals.ReportAsync(Set))).ValueOrThrow();
        var runs = report.Runs.ToDictionary(summary => summary.Run.Id);
        runs[before.Id].Items.Should().Be(4, "a removed item stays in the runs that scored it");
        runs[after.Id].Items.Should().Be(3);
        runs[after.Id].Correct.Should().Be(2, "the relabelled item now matches");
        Error(await EvalsAsync(evals => evals.RelabelItemAsync(refused.Id, "{}"))).Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task AddItem_ShouldFreezeTheSubjectSourcesInput_OrRefuseAnUnresolvedSubject()
    {
        await SetAsync();

        var item = (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft
        {
            Set = Set,
            Subject = "test:post:42",
            Expected = """{"relevance":"far"}""",
            Tags = ["lang:uz", "lang:uz", " "],
        }))).ValueOrThrow();
        var unresolved = await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Subject = "test:post:missing", Expected = "{}" }));
        var unknownPrefix = await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Subject = "other:1", Expected = "{}" }));
        var badExpected = await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Input = Post("x"), Expected = "[1]" }));

        item.Input.Should().BeEquivalentTo(new Dictionary<string, string?> { ["title"] = "Resolved post 42" });
        item.Tags.Should().Equal("lang:uz");
        Error(unresolved).Message.Should().Contain("test:post:missing");
        Error(unknownPrefix).Type.Should().Be(AppErrorType.NotFound);
        Error(badExpected).Type.Should().Be(AppErrorType.Validation);
        Error(await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = "no-set", Input = Post("x"), Expected = "{}" }))).Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task ExportImport_ShouldMoveTheSetWithItsItems_ToAnotherDatabase()
    {
        await SetAsync();
        var export = (await EvalsAsync(evals => evals.ExportSetAsync(Set))).ValueOrThrow();

        await ResetDatabaseAsync();
        await UseCaseAsync();
        var imported = (await EvalsAsync(evals => evals.ImportSetAsync(export))).ValueOrThrow();
        var again = (await EvalsAsync(evals => evals.ImportSetAsync(export))).ValueOrThrow();

        export.Format.Should().Be(EvalSetExport.FormatV1);
        imported.ScoreFields.Should().Equal("relevance");
        imported.SevereMisses.Should().Equal("relevance:close>none");
        again.Key.Should().Be(Set);
        var items = (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow();
        items.Should().BeEquivalentTo(export.Items, "an import keeps each item's id, input, expected answer and tags");
        Error(await EvalsAsync(evals => evals.ImportSetAsync(export with { Format = "other" }))).Type.Should().Be(AppErrorType.Validation);
        Error(await EvalsAsync(evals => evals.ImportSetAsync(export with { Set = export.Set with { UseCase = "missing" } }))).Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task PutSet_ShouldRefuse_AnUnknownUseCase_AndKeepItsItemsOnAReplace()
    {
        await SetAsync();

        var replaced = (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = Set, UseCase = "relevance", Note = "v2" }))).ValueOrThrow();

        replaced.ScoreFields.Should().BeEmpty();
        replaced.Note.Should().Be("v2");
        (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow().Should().HaveCount(4);
        (await EvalsAsync(evals => evals.ListSetsAsync())).ValueOrThrow().Should().ContainSingle();
        Error(await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = "x", UseCase = "missing" }))).Type.Should().Be(AppErrorType.NotFound);
        Error(await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = "Bad Key", UseCase = "relevance" }))).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>Creates the relevance use case and a set of four posts whose titles script the model's answer: one right,
    /// one severe miss, one schema-invalid and one refusal.</summary>
    private async Task SetAsync(PromptMode mode = PromptMode.Rewrite)
    {
        await UseCaseAsync(mode: mode);
        (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel
        {
            Key = Set,
            UseCase = "relevance",
            ScoreFields = ["relevance"],
            SevereMisses = ["relevance:close>none"],
            Note = "four scripted posts",
        }))).ValueOrThrow();

        foreach (var (subject, tag) in new[] { ("post:close", "lang:ru"), ("post:missed", "lang:ru"), ("post:broken", "lang:en"), ("post:refused", "lang:en") })
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft
            {
                Set = Set,
                Subject = subject,
                Input = Post(subject),
                Expected = """{"relevance":"close","kind":"runway"}""",
                Tags = [tag],
                LabelledBy = "max",
            }))).ValueOrThrow();
        }

        Caller.Respond = request => request.User switch
        {
            var user when user.EndsWith("post:close", StringComparison.Ordinal) => ScriptedAiModelCaller.Answer(Json("close"), 1000, 200, latencyMs: 100),
            var user when user.EndsWith("post:missed", StringComparison.Ordinal) => ScriptedAiModelCaller.Answer(Json("none"), 1000, 200, latencyMs: 200),
            var user when user.EndsWith("post:broken", StringComparison.Ordinal) => ScriptedAiModelCaller.Answer("close, a runway", 1000, 200, latencyMs: 300),
            _ => ScriptedAiModelCaller.Refusal() with { LatencyMs = 50 },
        };
    }

    private static AppError Error<T>(Result<T> result) where T : notnull =>
        result.Should().BeOfType<Result<T>.Failure>().Subject.Error;

    /// <summary>Resolves <c>test:post:{id}</c> subjects; <c>missing</c> does not exist.</summary>
    private sealed class PostSource : IEvalSubjectSource
    {
        public string Prefix => "test:post:";

        public Task<IReadOnlyDictionary<string, string?>?> ResolveAsync(string useCase, string subject, CancellationToken cancellationToken = default)
        {
            var id = subject[Prefix.Length..];
            return Task.FromResult<IReadOnlyDictionary<string, string?>?>(id == "missing" ? null : new Dictionary<string, string?> { ["title"] = $"Resolved post {id}" });
        }
    }
}
