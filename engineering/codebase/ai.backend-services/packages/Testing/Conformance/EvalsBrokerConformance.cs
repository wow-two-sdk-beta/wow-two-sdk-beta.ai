using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Ai.Testing.Conformance;

/// <summary>The conformance suite of <see cref="IEvalsBroker"/>: sets and items, a run now, a queued batch with its pins,
/// cancel, ratings, the report and export/import, as every implementation keeps them.</summary>
/// <remarks>
///   - derive a test class per implementation and supply both brokers; every test works on keys of its own, so the suite
///     runs against a shared store
///   - the suite runs on <see cref="Model"/>, a provider no form configures, so every item's call fails the same way in
///     every form and no test spends money; it checks what the broker records, never a model's answer
///   - the form under test runs no eval worker, so a queued run stays queued until the suite cancels it
/// </remarks>
public abstract class EvalsBrokerConformance
{
    /// <summary>Gets the implementation under test.</summary>
    protected abstract IEvalsBroker Broker { get; }

    /// <summary>Gets the prompt registry of the same form, which sets up each test's use case.</summary>
    protected abstract IPromptsBroker Prompts { get; }

    /// <summary>Gets the model every run of the suite names: a provider no form configures.</summary>
    protected virtual string Model => "conformance/unconfigured";

    /// <summary>A set reads back as it was put, lists with the others, and keeps its items on a replace.</summary>
    [Fact]
    public async Task PutSet_ShouldStoreTheSet_AndReadItBack()
    {
        var useCase = await UseCaseAsync();
        var key = NewKey();

        var put = Value(await Broker.PutSetAsync(new EvalSetModel
        {
            Key = key, UseCase = useCase, ScoreFields = ["relevance"], SevereMisses = ["relevance:close>none"], Note = "conformance",
        }));
        await ItemAsync(key, "first");
        var replaced = Value(await Broker.PutSetAsync(new EvalSetModel { Key = key, UseCase = useCase, Note = "replaced" }));

        put.ScoreFields.Should().Equal("relevance");
        put.Scope.Should().BeNull();
        replaced.Note.Should().Be("replaced");
        Value(await Broker.GetSetAsync(key)).Should().BeEquivalentTo(replaced);
        Value(await Broker.ListSetsAsync()).Should().Contain(set => set.Key == key);
        Value(await Broker.ListItemsAsync(key)).Should().ContainSingle("a replace keeps the items");
        Error(await Broker.GetSetAsync(NewKey())).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.PutSetAsync(new EvalSetModel { Key = NewKey(), UseCase = NewKey() })).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.PutSetAsync(new EvalSetModel { Key = "Bad Key", UseCase = useCase })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>An item freezes its input; a relabel changes its expected answer only; a removed item leaves the list but
    /// still reads by id.</summary>
    [Fact]
    public async Task Items_ShouldAddRelabelAndRemove()
    {
        var set = await SetAsync();
        var item = await ItemAsync(set, "Fashion week", "lang:en");

        var relabelled = Value(await Broker.RelabelItemAsync(item.Id, """{"relevance":"far"}""", "conformance"));
        var removed = Value(await Broker.RemoveItemAsync(item.Id));

        item.Input.Should().BeEquivalentTo(new Dictionary<string, string?> { ["title"] = "Fashion week" });
        item.Tags.Should().Equal("lang:en");
        relabelled.Expected.Should().Be("""{"relevance":"far"}""");
        relabelled.LabelledBy.Should().Be("conformance");
        relabelled.Input.Should().BeEquivalentTo(item.Input);
        removed.Should().BeTrue();
        Value(await Broker.RemoveItemAsync(item.Id)).Should().BeFalse("it is gone already");
        Value(await Broker.ListItemsAsync(set)).Should().BeEmpty();
        Value(await Broker.GetItemAsync(item.Id)).Id.Should().Be(item.Id);
        Error(await Broker.GetItemAsync(Guid.NewGuid())).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.AddItemAsync(new EvalItemDraft { Set = set, Input = Title("x"), Expected = "[1]" })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.AddItemAsync(new EvalItemDraft { Set = NewKey(), Input = Title("x"), Expected = "{}" })).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A run now takes every item, pins its batch and prompt, scores each item, and ends succeeded even when every
    /// call failed: completed work with no usable answer.</summary>
    [Fact]
    public async Task Run_ShouldScoreEveryItem_AndReport()
    {
        var set = await SetAsync(items: 3);

        var run = Value(await Broker.RunAsync(new EvalRunRequest { Set = set, Model = Model, Note = "now", RequestedBy = "conformance" }));

        run.Status.Should().Be(EvalRunStatus.Succeeded);
        run.ItemsTotal.Should().Be(3);
        run.ItemsDone.Should().Be(3);
        run.BatchId.Should().NotBeNull();
        run.PromptId.Should().NotBeNull();
        run.PromptVersion.Should().Be(1);
        run.RequestedBy.Should().Be("conformance");
        run.FinishedAtUtc.Should().NotBeNull();
        Value(await Broker.GetRunAsync(run.Id)).Should().BeEquivalentTo(run);
        Value(await Broker.ListRunsAsync(set)).Should().ContainSingle().Which.Id.Should().Be(run.Id);
        Value(await Broker.ListRunsAsync(new EvalRunQuery { BatchId = run.BatchId })).Should().ContainSingle().Which.Id.Should().Be(run.Id);

        var report = Value(await Broker.ReportAsync(set));
        var summary = report.Runs.Should().ContainSingle().Subject;
        summary.Items.Should().Be(3);
        summary.Failed.Should().Be(3);
        summary.Answered.Should().Be(0);
        summary.SpentMicroUsd.Should().Be(0);
        report.Items.Should().HaveCount(3).And.OnlyContain(item => item.Outcome == AiOutcome.Failed && item.PromptHash == run.PromptHash);
        Error(await Broker.ReportAsync(set, [Guid.NewGuid()])).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.RunAsync(new EvalRunRequest { Set = set, Model = "no-slash" })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.RunAsync(new EvalRunRequest { Set = NewKey(), Model = Model })).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A queued matrix pins one sample for all its cells and waits for a worker; a cancel ends a queued run at once
    /// and leaves an ended one as it is.</summary>
    [Fact]
    public async Task Queue_ShouldPinTheSample_AndCancel()
    {
        var set = await SetAsync(items: 4);
        var models = new[] { Model, Model.Replace("conformance/", "conformance-b/", StringComparison.Ordinal) };

        var batch = Value(await Broker.QueueAsync(new EvalMatrixRequest { Set = set, Models = models, Count = 2, Seed = 3, RequestedBy = "conformance" }));

        batch.Set.Should().Be(set);
        batch.ItemIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        batch.ItemIds.Should().BeSubsetOf(Value(await Broker.ListItemsAsync(set)).Select(item => item.Id));
        batch.Sample.Should().BeEquivalentTo(new EvalSampleModel { Count = 2, Seed = 3 });
        batch.Runs.Should().HaveCount(2).And.OnlyContain(run => run.Status == EvalRunStatus.Queued && run.BatchId == batch.Id && run.ItemsTotal == 2);
        Value(await Broker.GetRunAsync(batch.Runs[0].Id)).Status.Should().Be(EvalRunStatus.Queued);

        var canceled = Value(await Broker.CancelRunAsync(batch.Runs[0].Id));
        var again = Value(await Broker.CancelRunAsync(batch.Runs[0].Id));

        canceled.Status.Should().Be(EvalRunStatus.Canceled);
        canceled.FinishedAtUtc.Should().NotBeNull();
        again.Status.Should().Be(EvalRunStatus.Canceled);
        Value(await Broker.ListRunsAsync(new EvalRunQuery { BatchId = batch.Id, Status = EvalRunStatus.Canceled })).Should().ContainSingle();
        Value(await Broker.CancelRunAsync(batch.Runs[1].Id)).Status.Should().Be(EvalRunStatus.Canceled);
        Error(await Broker.CancelRunAsync(Guid.NewGuid())).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.QueueAsync(new EvalMatrixRequest { Set = set, Models = [] })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.QueueAsync(new EvalMatrixRequest { Set = set, Models = [Model], Tags = ["no-such-tag"] })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.QueueAsync(new EvalMatrixRequest { Set = NewKey(), Models = [Model] })).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A rating shows on the item and a later one replaces it; a rating outside 1–5, an unknown run or an item the
    /// run did not score is refused.</summary>
    [Fact]
    public async Task Rate_ShouldReplaceTheRating()
    {
        var set = await SetAsync(items: 1);
        var run = Value(await Broker.RunAsync(new EvalRunRequest { Set = set, Model = Model }));
        var item = Value(await Broker.ListItemsAsync(set)).Single();

        Value(await Broker.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = item.Id, Rating = 2 }));
        var rated = Value(await Broker.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = item.Id, Rating = 4, Comment = "fine", By = "conformance" }));

        rated.Rating.Should().Be(4);
        rated.Comment.Should().Be("fine");
        Value(await Broker.ReportAsync(set)).Runs.Single().MeanRating.Should().Be(4);
        Error(await Broker.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = item.Id, Rating = 6 })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.RateAsync(new EvalRatingDraft { RunId = Guid.NewGuid(), ItemId = item.Id, Rating = 3 })).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.RateAsync(new EvalRatingDraft { RunId = run.Id, ItemId = Guid.NewGuid(), Rating = 3 })).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>An export carries the set with its items; an import under a new key brings them back.</summary>
    [Fact]
    public async Task ExportImport_ShouldCarryTheSetWithItsItems()
    {
        var set = await SetAsync(items: 2);

        var export = Value(await Broker.ExportSetAsync(set));
        var key = NewKey();
        var imported = Value(await Broker.ImportSetAsync(export with
        {
            Set = export.Set with { Key = key },
            Items = [.. export.Items.Select(item => item with { Id = Guid.Empty })],
        }));

        export.Format.Should().Be(EvalSetExport.FormatV1);
        export.Items.Should().HaveCount(2);
        imported.Key.Should().Be(key);
        Value(await Broker.ListItemsAsync(key)).Select(item => item.Input["title"]).Should().BeEquivalentTo(export.Items.Select(item => item.Input["title"]));
        Error(await Broker.ImportSetAsync(export with { Format = "other" })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.ExportSetAsync(NewKey())).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>An item keeps its image references, never bytes, through a read, a list, an export and an import; a run
    /// whose prompt takes no image ignores them, unreadable ones included; a malformed reference is refused.</summary>
    [Fact]
    public async Task Items_ShouldKeepTheirImageReferences_ThroughExportAndImport()
    {
        var set = await SetAsync();
        AiImageRef[] images = [new() { Key = "conformance:poster-1", MediaType = "image/png" }, new() { Uri = "https://images.example.test/poster-2.jpg" }];

        var item = Value(await Broker.AddItemAsync(new EvalItemDraft { Set = set, Input = Title("poster"), Images = images, Expected = """{"relevance":"close"}""" }));
        var export = Value(await Broker.ExportSetAsync(set));
        var key = NewKey();
        Value(await Broker.ImportSetAsync(export with { Set = export.Set with { Key = key }, Items = [.. export.Items.Select(one => one with { Id = Guid.Empty })] }));
        var run = Value(await Broker.RunAsync(new EvalRunRequest { Set = set, Model = Model }));

        item.Images.Should().Equal(images);
        Value(await Broker.GetItemAsync(item.Id)).Images.Should().Equal(images);
        Value(await Broker.ListItemsAsync(set)).Single().Images.Should().Equal(images);
        export.Items.Single().Images.Should().Equal(images);
        Value(await Broker.ListItemsAsync(key)).Single().Images.Should().Equal(images);
        run.Status.Should().Be(EvalRunStatus.Succeeded, "the prompt takes no image, so none is resolved");
        Error(await Broker.AddItemAsync(new EvalItemDraft
        {
            Set = set, Input = Title("x"), Images = [new AiImageRef { Uri = "https://a.example.test/x.png", Key = "both" }], Expected = "{}",
        })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>Creates a rewrite use case with one prompt that renders <c>{{title}}</c>, routed to <see cref="Model"/>.</summary>
    /// <returns>Its key.</returns>
    protected async Task<string> UseCaseAsync()
    {
        var key = NewKey();
        Value(await Prompts.PutUseCaseAsync(new UseCaseModel { Key = key, PromptMode = PromptMode.Rewrite, Route = [Model] }));
        Value(await Prompts.SavePromptAsync(new PromptDraft { UseCase = key, SystemTemplate = "You rate posts.", UserTemplate = "Post: {{title}}" }));
        return key;
    }

    /// <summary>Creates a set over a use case of its own, scored by relevance, with <paramref name="items"/> items.</summary>
    /// <returns>Its key.</returns>
    protected async Task<string> SetAsync(int items = 0)
    {
        var key = NewKey();
        Value(await Broker.PutSetAsync(new EvalSetModel { Key = key, UseCase = await UseCaseAsync(), ScoreFields = ["relevance"] }));
        for (var index = 0; index < items; index++)
            await ItemAsync(key, $"post {index}");
        return key;
    }

    /// <summary>Adds an item expecting <c>close</c>.</summary>
    protected async Task<EvalItemModel> ItemAsync(string set, string title, params string[] tags) =>
        Value(await Broker.AddItemAsync(new EvalItemDraft { Set = set, Input = Title(title), Expected = """{"relevance":"close"}""", Tags = tags }));

    /// <summary>Creates a fresh key.</summary>
    protected static string NewKey() => $"conf-{Guid.NewGuid():N}"[..29];

    private static Dictionary<string, string?> Title(string title) => new() { ["title"] = title };

    /// <summary>Returns the success value, failing the test with the broker's own error otherwise.</summary>
    private static T Value<T>(Result<T> result) where T : notnull =>
        result is Result<T>.Success success
            ? success.Value
            : throw new Xunit.Sdk.XunitException(((Result<T>.Failure)result).Error.Message);

    /// <summary>Returns the failure's error, failing the test on a success.</summary>
    private static AppError Error<T>(Result<T> result) where T : notnull =>
        result is Result<T>.Failure failure
            ? failure.Error
            : throw new Xunit.Sdk.XunitException($"Expected a failure, got {((Result<T>.Success)result).Value}.");
}
