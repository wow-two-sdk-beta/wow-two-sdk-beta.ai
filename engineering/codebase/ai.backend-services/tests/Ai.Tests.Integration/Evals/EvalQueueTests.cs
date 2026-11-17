using System.Collections.Concurrent;
using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Evals;

/// <summary>Queued comparisons over PostgreSQL with scripted providers: what a batch pins and a worker repeats, claims that
/// never double, cancels between items, orphans, a provider down for every item, the cap, and pins that no longer hold.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class EvalQueueTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    private const string Set = "afisha-6";

    /// <inheritdoc />
    protected override void Configure(AiModuleOptions options)
    {
        base.Configure(options);
        options.MaxEvalItemCalls = 12;
    }

    /// <inheritdoc />
    protected override void AddServices(IServiceCollection collection) =>
        collection.AddAiEvalWorker().Configure(options => options.PollInterval = TimeSpan.FromMilliseconds(100));

    [Fact]
    public async Task Queue_ShouldPinOneSampleForEveryCell_AndTheWorkerShouldRunExactlyIt()
    {
        await SetAsync(PromptMode.Rewrite);
        Caller.Respond = request => ScriptedAiModelCaller.Answer(Json(request.User.Contains("fashion", StringComparison.Ordinal) ? "close" : "none"));

        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest
        {
            Set = Set, Models = [Haiku, Luna], Count = 3, Seed = 7, Note = "pinned", RequestedBy = "claude",
        }))).ValueOrThrow();

        batch.ItemIds.Should().HaveCount(3);
        batch.ScoreFields.Should().Equal("relevance");
        batch.Sample.Should().BeEquivalentTo(new EvalSampleModel { Count = 3, Seed = 7 });
        batch.Runs.Should().HaveCount(2).And.OnlyContain(run => run.Status == EvalRunStatus.Queued && run.ItemsTotal == 3 && run.BatchId == batch.Id
            && run.RequestedBy == "claude" && run.PromptId != null);
        var pinnedHash = batch.Runs[0].PromptHash;

        // After queueing: the prompt is rewritten, a pinned item relabelled, the rules changed and an item added.
        await SaveAsync("relevance", "Rewritten: {{title}}");
        var pinnedItem = batch.ItemIds[0];
        (await EvalsAsync(evals => evals.RelabelItemAsync(pinnedItem, """{"relevance":"far"}"""))).ValueOrThrow();
        (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = Set, UseCase = "relevance", ScoreFields = ["kind"] }))).ValueOrThrow();
        (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Input = Post("late fashion"), Expected = Json("close") }))).ValueOrThrow();

        await RunWorkerUntilAsync(batch.Id, EvalRunStatus.Succeeded);

        Caller.Requests.Should().HaveCount(6, "two cells × three pinned items, nothing added later");
        Caller.Requests.Should().OnlyContain(request => request.User.StartsWith("Post: ", StringComparison.Ordinal), "the pinned text, not the rewrite");
        var items = (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow().ToDictionary(item => item.Id);
        Caller.Requests.Select(request => request.User).Distinct().Should().BeEquivalentTo(batch.ItemIds.Select(id => "Post: " + items[id].Input["title"]));

        var report = (await EvalsAsync(evals => evals.ReportAsync(Set, [.. batch.Runs.Select(run => run.Id)], withAnswers: true))).ValueOrThrow();
        report.Items.Should().HaveCount(6).And.OnlyContain(item => item.PromptHash == pinnedHash);
        report.Items.Where(item => item.ItemId == pinnedItem).Should().OnlyContain(item => item.Expected != """{"relevance":"far"}""",
            "a relabel after queueing reaches later batches only");
        report.Runs.Should().OnlyContain(summary => summary.Run.Status == EvalRunStatus.Succeeded && summary.Run.ItemsDone == 3 && summary.Items == 3);
        report.Items.Should().OnlyContain(item => item.Correct == (item.Expected.Contains("close") == item.Answer!.Contains("close")),
            "scored by relevance, the rule pinned when queued, not by kind");

        // The next batch pins what holds now: the rewritten prompt.
        var again = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], Count = 3, Seed = 7 }))).ValueOrThrow();
        again.Runs.Single().PromptHash.Should().NotBe(pinnedHash);
        again.ItemIds.Should().BeSubsetOf(items.Keys);
    }

    [Fact]
    public async Task Queue_ShouldPinTheLabelsVersion_WhenItWasQueued()
    {
        await SetAsync(PromptMode.Versioned);
        await SaveAsync("relevance", "v2 Post: {{title}}");
        (await PromptsAsync(prompts => prompts.MoveLabelAsync("relevance", AiConstants.ProductionLabel, 1))).ValueOrThrow();
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("none"));

        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], Labels = [AiConstants.ProductionLabel], Count = 2 }))).ValueOrThrow();
        (await PromptsAsync(prompts => prompts.MoveLabelAsync("relevance", AiConstants.ProductionLabel, 2))).ValueOrThrow();
        await RunWorkerUntilAsync(batch.Id, EvalRunStatus.Succeeded);

        batch.Runs.Single().PromptVersion.Should().Be(1);
        Caller.Requests.Should().HaveCount(2).And.OnlyContain(request => request.User.StartsWith("Post: ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Claim_ShouldNeverHandTheSameRunToTwoInstances()
    {
        await SetAsync(PromptMode.Versioned);
        await SaveAsync("relevance", "v2 Post: {{title}}");
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku, Luna], Versions = [1, 2], Count = 1 }))).ValueOrThrow();
        batch.Runs.Should().HaveCount(4);

        // A second instance: its own container and pool over the same database.
        var second = new ServiceCollection().AddLogging();
        second.AddAi().Configure(options => options.ConnectionString = ConnectionString);
        await using var other = second.BuildServiceProvider();
        var queues = new[] { Services.GetRequiredService<EvalRunQueue>(), other.GetRequiredService<EvalRunQueue>() };
        var claimed = new ConcurrentBag<Guid>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            while (await queues[index % 2].ClaimAsync(CancellationToken.None) is { } run)
                claimed.Add(run);
        })));

        claimed.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(batch.Runs.Select(run => run.Id));
        var runs = (await EvalsAsync(evals => evals.ListRunsAsync(new EvalRunQuery { BatchId = batch.Id }))).ValueOrThrow();
        runs.Should().OnlyContain(run => run.Status == EvalRunStatus.Running && run.HeartbeatAtUtc != null);
    }

    [Fact]
    public async Task Cancel_ShouldStopARunningRunBetweenItems_AndKeepWhatItScored()
    {
        await SetAsync(PromptMode.Rewrite);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Caller.Respond = _ =>
        {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(30));
            return ScriptedAiModelCaller.Answer(Json("close"));
        };
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku] }))).ValueOrThrow();
        var runId = batch.Runs.Single().Id;
        var queued = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Luna] }))).ValueOrThrow().Runs.Single().Id;
        (await EvalsAsync(evals => evals.CancelRunAsync(queued))).ValueOrThrow().Status.Should().Be(EvalRunStatus.Canceled, "a queued run cancels at once");

        (await Services.GetRequiredService<EvalRunQueue>().ClaimAsync(CancellationToken.None)).Should().Be(runId, "the canceled run is never claimed");
        var running = InScopeAsync(provider => provider.GetRequiredService<ModuleEvalsBroker>().ExecuteQueuedAsync(runId, CancellationToken.None));
        started.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
        var canceled = (await EvalsAsync(evals => evals.CancelRunAsync(runId))).ValueOrThrow();
        release.Set();
        var ended = await running;

        canceled.Status.Should().Be(EvalRunStatus.Canceled);
        ended.Status.Should().Be(EvalRunStatus.Canceled);
        ended.ItemsDone.Should().Be(1, "the item in flight finished; no later item started");
        ended.ItemsTotal.Should().Be(6);
        Caller.Requests.Should().HaveCount(1);
        (await EvalsAsync(evals => evals.ReportAsync(Set, [runId]))).ValueOrThrow().Items.Should().ContainSingle();
        (await EvalsAsync(evals => evals.CancelRunAsync(runId))).ValueOrThrow().Status.Should().Be(EvalRunStatus.Canceled, "an ended run stays as it is");
    }

    [Fact]
    public async Task Worker_ShouldFailARunWhoseHeartbeatWentStale_AsOrphaned()
    {
        await SetAsync(PromptMode.Rewrite);
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku] }))).ValueOrThrow();
        var queue = Services.GetRequiredService<EvalRunQueue>();
        (await queue.ClaimAsync(CancellationToken.None)).Should().Be(batch.Runs.Single().Id, "a process claims it, then dies");

        (await queue.FailOrphansAsync(TimeSpan.FromMinutes(2), CancellationToken.None)).Should().BeEmpty("its heartbeat is fresh");
        Clock.Advance(TimeSpan.FromMinutes(3));
        var orphans = await queue.FailOrphansAsync(TimeSpan.FromMinutes(2), CancellationToken.None);

        orphans.Should().Equal(batch.Runs.Single().Id);
        var run = (await EvalsAsync(evals => evals.GetRunAsync(orphans[0]))).ValueOrThrow();
        run.Status.Should().Be(EvalRunStatus.Failed);
        run.Error.Should().StartWith("Orphaned");
        run.FinishedAtUtc.Should().Be(Clock.GetUtcNow());
    }

    [Fact]
    public async Task Worker_ShouldRecordEveryItem_WhenEveryProviderFails()
    {
        await SetAsync(PromptMode.Rewrite);
        Caller.Respond = _ => ScriptedAiModelCaller.Error(AiErrorKind.Unauthorized, "bad key");
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku, Luna], Count = 3 }))).ValueOrThrow();

        await RunWorkerUntilAsync(batch.Id, EvalRunStatus.Succeeded);

        var report = (await EvalsAsync(evals => evals.ReportAsync(Set, [.. batch.Runs.Select(run => run.Id)]))).ValueOrThrow();
        report.Runs.Should().HaveCount(2).And.OnlyContain(summary => summary.Items == 3 && summary.Failed == 3 && summary.Answered == 0
            && summary.Accuracy == 0 && summary.SpentMicroUsd == 0 && summary.Run.ItemsDone == 3, "completed work, no usable result");
        report.Items.Should().HaveCount(6).And.OnlyContain(item => item.Outcome == AiOutcome.Failed && item.InferenceId != null);
        var logged = (await InferencesAsync(log => log.ListAsync(new InferenceQuery { EvalRunId = batch.Runs[0].Id }))).ValueOrThrow();
        logged.Should().HaveCount(3).And.OnlyContain(inference => inference.ErrorKind == AiErrorKind.Unauthorized);
    }

    [Fact]
    public async Task Queue_ShouldRefuse_OverTheCap_OrASampleThatKeepsNothing()
    {
        await SetAsync(PromptMode.Rewrite);

        var over = await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku, Luna, "openai/gpt-6.1-terra"], Count = 5 }));
        var empty = await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], Tags = ["lang:none"] }));
        var unknown = await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], ItemIds = [Guid.NewGuid()] }));
        var noModel = await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [] }));
        var nowOver = await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku, Count = 13 }));

        Error(over).Message.Should().Contain("15 item calls").And.Contain("cap of 12");
        Error(empty).Type.Should().Be(AppErrorType.Validation);
        Error(unknown).Type.Should().Be(AppErrorType.Validation);
        Error(noModel).Type.Should().Be(AppErrorType.Validation);
        nowOver.Should().BeOfType<Result<EvalRunModel>.Success>("a sample of 13 keeps the set's 6 items");
        (await EvalsAsync(evals => evals.ListRunsAsync(new EvalRunQuery { Status = EvalRunStatus.Queued }))).ValueOrThrow().Should().BeEmpty();
    }

    [Fact]
    public async Task Worker_ShouldFailARun_WhosePinnedInputChangedOrWasPurged()
    {
        await SetAsync(PromptMode.Rewrite);
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], Count = 2 }))).ValueOrThrow();
        await using (var connection = new NpgsqlConnection(ConnectionString))
            await connection.ExecuteAsync("update ai.ai_eval_items set input = '{}' where id = @Id", new { Id = batch.ItemIds[1] });

        await RunWorkerUntilAsync(batch.Id, EvalRunStatus.Failed);

        var run = (await EvalsAsync(evals => evals.GetRunAsync(batch.Runs.Single().Id))).ValueOrThrow();
        run.Error.Should().Contain("1 of the 2 pinned items changed or were purged");
        Caller.Requests.Should().BeEmpty("a run that cannot repeat sends nothing");
    }

    /// <summary>Starts the module's worker and waits until every run of the batch reads <paramref name="status"/>.</summary>
    private async Task RunWorkerUntilAsync(Guid batchId, EvalRunStatus status)
    {
        var worker = Services.GetServices<IHostedService>().OfType<AiEvalWorker>().Single();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var runs = (await EvalsAsync(evals => evals.ListRunsAsync(new EvalRunQuery { BatchId = batchId }))).ValueOrThrow();
                if (runs.All(run => run.Status == status))
                    return;
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"The batch's runs read {string.Join(", ", runs.Select(run => $"{run.Status} {run.Error}"))}.");
                await Task.Delay(100);
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Creates the relevance use case and a set of six posts, three about fashion, scored by relevance.</summary>
    private async Task SetAsync(PromptMode mode)
    {
        await UseCaseAsync(mode: mode);
        (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = Set, UseCase = "relevance", ScoreFields = ["relevance"] }))).ValueOrThrow();
        foreach (var title in new[] { "fashion week", "car fair", "fashion expo", "book fair", "fashion night", "food fest" })
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft
            {
                Set = Set,
                Subject = "post:" + title.Replace(' ', '-'),
                Input = Post(title),
                Expected = Json(title.Contains("fashion", StringComparison.Ordinal) ? "close" : "none"),
            }))).ValueOrThrow();
        }
    }

    private static AppError Error<T>(Result<T> result) where T : notnull =>
        result.Should().BeOfType<Result<T>.Failure>().Subject.Error;
}
