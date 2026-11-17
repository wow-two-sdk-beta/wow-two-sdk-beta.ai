using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Evals;

/// <summary>Eval items with images over PostgreSQL: references kept and exported without bytes, a subject source's images,
/// a batch pinning the resolved hashes, and a run that fails once an image changed since it was queued.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class EvalImageTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    private const string Set = "posters";

    private readonly MemoryImages images = new();

    /// <inheritdoc />
    protected override void AddServices(IServiceCollection collection)
    {
        collection.AddSingleton<IAiImageResolver>(images);
        collection.AddSingleton<IEvalSubjectSource>(new PosterSource());
    }

    [Fact]
    public async Task Items_ShouldKeepTheirImageReferences_AndExportThemWithoutBytes()
    {
        await SetAsync(maxImages: 1);
        AiImageRef[] refs = [MemoryImages.Ref("a"), new AiImageRef { Uri = "https://cdn.example.test/a.jpg", MediaType = "image/jpeg" }];

        var item = (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Input = Post("a"), Images = refs, Expected = Json("close") }))).ValueOrThrow();
        var export = (await EvalsAsync(evals => evals.ExportSetAsync(Set))).ValueOrThrow();

        item.Images.Should().Equal(refs);
        (await EvalsAsync(evals => evals.GetItemAsync(item.Id))).ValueOrThrow().Images.Should().Equal(refs);
        export.Items.Single().Images.Should().Equal(refs);
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            (await connection.ExecuteScalarAsync<string>("select images::text from ai.ai_eval_items where id = @Id", new { item.Id }))
                .Should().Be("""[{"key": "fss:a"}, {"uri": "https://cdn.example.test/a.jpg", "mediaType": "image/jpeg"}]""", "references only, never bytes");
        }

        await ResetDatabaseAsync();
        await SetAsync(maxImages: 1, withSet: false);
        (await EvalsAsync(evals => evals.ImportSetAsync(export))).ValueOrThrow();
        (await EvalsAsync(evals => evals.ListItemsAsync(Set))).ValueOrThrow().Single().Images.Should().Equal(refs);
        images.Asked.Should().BeEmpty("adding, exporting and importing resolve nothing");
    }

    [Fact]
    public async Task Items_ShouldTakeTheSubjectSourcesImages_UnlessTheDraftNamesItsOwn()
    {
        await SetAsync(maxImages: 1);

        var fromSource = (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Subject = "poster:7", Expected = Json("close") }))).ValueOrThrow();
        var own = (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft
        {
            Set = Set, Subject = "poster:8", Images = [MemoryImages.Ref("mine")], Expected = Json("close"),
        }))).ValueOrThrow();
        var tooMany = await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft
        {
            Set = Set, Input = Post("x"), Images = [.. Enumerable.Range(0, 5).Select(index => MemoryImages.Ref(index.ToString()))], Expected = Json("close"),
        }));

        fromSource.Input["title"].Should().Be("Poster 7");
        fromSource.Images.Should().Equal(MemoryImages.Ref("poster-7"));
        own.Images.Should().Equal(MemoryImages.Ref("mine"));
        tooMany.Should().BeOfType<Result<EvalItemModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public async Task Run_ShouldSendEachItemsImages_AndPinTheirHashesOnTheBatch()
    {
        await SetAsync(maxImages: 1);
        var a = images.Put("a", seed: 1);
        var b = images.Put("b", seed: 2);
        await ItemAsync("a", MemoryImages.Ref("a"));
        await ItemAsync("b", MemoryImages.Ref("b"));
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var run = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();

        run.Status.Should().Be(EvalRunStatus.Succeeded);
        run.ItemsDone.Should().Be(2);
        Caller.Requests.Select(request => request.Images.Single().Data.ToArray()).Should().BeEquivalentTo([a, b]);
        await using var connection = new NpgsqlConnection(ConnectionString);
        var pinned = (await connection.QueryAsync<string[]>("select image_hashes from ai.ai_eval_batch_items where batch_id = @BatchId", new { run.BatchId })).ToList();
        pinned.Select(hashes => hashes.Single()).Should().BeEquivalentTo([MemoryImages.Sha256(a), MemoryImages.Sha256(b)]);
        var logged = (await connection.QueryAsync<(int Count, string[] Hashes)>("select image_count, image_hashes from ai.ai_inferences where eval_run_id = @Id", new { run.Id })).ToList();
        logged.Should().HaveCount(2).And.OnlyContain(row => row.Count == 1 && row.Hashes.Length == 1);
    }

    [Fact]
    public async Task Queue_ShouldFailTheRun_WhenAnImageChangedSinceItWasQueued()
    {
        await SetAsync(maxImages: 1);
        images.Put("a", seed: 1);
        images.Put("b", seed: 2);
        await ItemAsync("a", MemoryImages.Ref("a"));
        await ItemAsync("b", MemoryImages.Ref("b"));
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        var batch = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku] }))).ValueOrThrow();

        images.Put("b", seed: 9);
        var ended = await ExecuteAsync(batch.Runs.Single().Id);

        ended.Status.Should().Be(EvalRunStatus.Failed);
        ended.Error.Should().Be("The images of 1 pinned item changed or could not be read since the run was queued, so it would not repeat; queue it again.");
        Caller.Requests.Should().OnlyContain(request => request.Images.Single().Data.ToArray()[8] == 1, "the changed image never reaches the model");

        images.Put("b", seed: 2);
        var again = (await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku], Fresh = true }))).ValueOrThrow();
        (await ExecuteAsync(again.Runs.Single().Id)).Status.Should().Be(EvalRunStatus.Succeeded, "the image is back as it was pinned");
    }

    [Fact]
    public async Task Queue_ShouldRefuse_WhenAnItemsImageCannotBeRead_AndResolveNothingForATextPrompt()
    {
        await SetAsync(maxImages: 1);
        images.Put("a");
        await ItemAsync("a", MemoryImages.Ref("a"));
        await ItemAsync("gone", MemoryImages.Ref("gone"));

        var refused = await EvalsAsync(evals => evals.QueueAsync(new EvalMatrixRequest { Set = Set, Models = [Haiku] }));

        var error = refused.Should().BeOfType<Result<EvalBatchModel>.Failure>().Subject.Error;
        error.Type.Should().Be(AppErrorType.NotFound);
        error.Message.Should().StartWith("Eval item '").And.EndWith("The image 'fss:gone' could not be read: no image resolver handles that key.");
        (await EvalsAsync(evals => evals.ListRunsAsync(Set))).ValueOrThrow().Should().BeEmpty("nothing is queued");

        await SaveAsync("relevance", "Post: {{title}}", parameters: new PromptParameters());
        var asked = images.Asked.Count;
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));
        var text = (await EvalsAsync(evals => evals.RunAsync(new EvalRunRequest { Set = Set, Model = Haiku }))).ValueOrThrow();

        text.Status.Should().Be(EvalRunStatus.Succeeded, "a prompt that takes no image ignores the items' images");
        images.Asked.Should().HaveCount(asked);
        Caller.Requests.Should().OnlyContain(request => request.Images.Count == 0);
    }

    private async Task SetAsync(int? maxImages, bool withSet = true)
    {
        await UseCaseAsync();
        await SaveAsync("relevance", "Post: {{title}}", parameters: new PromptParameters { MaxImages = maxImages });
        if (withSet)
            (await EvalsAsync(evals => evals.PutSetAsync(new EvalSetModel { Key = Set, UseCase = "relevance", ScoreFields = ["relevance"] }))).ValueOrThrow();
    }

    private async Task ItemAsync(string title, params AiImageRef[] refs)
    {
        Clock.Advance(TimeSpan.FromSeconds(1));
        (await EvalsAsync(evals => evals.AddItemAsync(new EvalItemDraft { Set = Set, Input = Post(title), Images = refs, Expected = Json("close") }))).ValueOrThrow();
    }

    private async Task<EvalRunModel> ExecuteAsync(Guid runId)
    {
        (await Services.GetRequiredService<EvalRunQueue>().ClaimAsync(CancellationToken.None)).Should().Be(runId);
        return await InScopeAsync(provider => provider.GetRequiredService<ModuleEvalsBroker>().ExecuteQueuedAsync(runId, CancellationToken.None));
    }

    /// <summary>A subject source that returns images: each <c>poster:{id}</c> with its poster behind <c>fss:poster-{id}</c>.</summary>
    private sealed class PosterSource : IEvalSubjectSource
    {
        public string Prefix => "poster:";

        public Task<IReadOnlyDictionary<string, string?>?> ResolveAsync(string useCase, string subject, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The module asks ResolveInputAsync.");

        public Task<EvalSubjectInput?> ResolveInputAsync(string useCase, string subject, CancellationToken cancellationToken = default)
        {
            var id = subject[Prefix.Length..];
            return Task.FromResult<EvalSubjectInput?>(new EvalSubjectInput
            {
                Variables = new Dictionary<string, string?> { ["title"] = $"Poster {id}" },
                Images = [MemoryImages.Ref("poster-" + id)],
            });
        }
    }
}
