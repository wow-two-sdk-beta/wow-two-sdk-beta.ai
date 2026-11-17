using Ai.Tests.Integration.Infrastructure;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Runs;

/// <summary>Calls with images over PostgreSQL: a host resolver behind <c>fss:</c> keys, the scripted caller seeing the
/// bytes, the log keeping their hashes, the cache telling images apart, and every refusal before any call.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public sealed class ImageRunTests(MigratorPostgresFixture fixture) : ModuleTestBase(fixture)
{
    private const string UseCase = "poster";

    private readonly MemoryImages images = new();

    /// <inheritdoc />
    protected override void Configure(AiModuleOptions options)
    {
        base.Configure(options);
        options.MaxImageBytes = 64;
    }

    /// <inheritdoc />
    protected override void AddServices(IServiceCollection collection) => collection.AddSingleton<IAiImageResolver>(images);

    [Fact]
    public async Task Run_ShouldSendTheImageBytes_AndLogTheirHashesNeverTheBytes()
    {
        await PosterAsync(maxImages: 2);
        var first = images.Put("1", seed: 1);
        var second = images.Put("2", seed: 2);
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close"), inputTokens: 1700));

        var run = (await RunAsync(Request(MemoryImages.Ref("1"), MemoryImages.Ref("2")))).ValueOrThrow();

        var request = Caller.Requests.Should().ContainSingle().Subject;
        request.Images.Select(image => image.Data.ToArray()).Should().BeEquivalentTo([first, second], options => options.WithStrictOrdering());
        request.Images.Should().OnlyContain(image => image.MediaType == "image/png");
        run.Outcome.Should().Be(AiOutcome.Succeeded);
        run.Final.ImageCount.Should().Be(2);
        run.Final.ImageHashes.Should().Equal(MemoryImages.Sha256(first), MemoryImages.Sha256(second));
        run.Final.InputTokens.Should().Be(1700, "image tokens are input tokens");

        var logged = (await InferencesAsync(log => log.ListAsync(new InferenceQuery { UseCase = UseCase }))).ValueOrThrow().Single();
        logged.ImageCount.Should().Be(2);
        logged.ImageHashes.Should().Equal(run.Final.ImageHashes);
        logged.InputHash.Should().Be(run.Final.InputHash);
    }

    [Fact]
    public async Task Run_ShouldSendOnlyThePromptsFirstImages_AndResolveNoMore()
    {
        await PosterAsync(maxImages: 1);
        images.Put("1");
        images.Put("2", seed: 2);
        Caller.Enqueue(ScriptedAiModelCaller.Answer(Json("close")));

        var run = (await RunAsync(Request(MemoryImages.Ref("1"), MemoryImages.Ref("2"), MemoryImages.Ref("missing")))).ValueOrThrow();

        Caller.Requests.Single().Images.Should().ContainSingle();
        run.Final.ImageCount.Should().Be(1);
        images.Asked.Should().Equal("fss:1");
    }

    [Fact]
    public async Task Run_ShouldIgnoreTheImages_WhenThePromptTakesNone()
    {
        await PosterAsync(maxImages: null);
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var text = (await RunAsync(Request() with { Fresh = true })).ValueOrThrow();
        var withImages = (await RunAsync(Request(MemoryImages.Ref("never-stored"), new AiImageRef { Uri = "https://unreachable.invalid/x.png" }) with { Fresh = true })).ValueOrThrow();

        var nullImages = (await RunAsync(Request() with { Images = null!, Fresh = true })).ValueOrThrow();

        withImages.Outcome.Should().Be(AiOutcome.Succeeded, "unreadable images do not matter to a text prompt");
        nullImages.Outcome.Should().Be(AiOutcome.Succeeded, "a wire body's null images read as none");
        Caller.Requests.Should().HaveCount(3).And.OnlyContain(request => request.Images.Count == 0);
        images.Asked.Should().BeEmpty("nothing is resolved for a prompt that takes no image");
        withImages.Final.InputHash.Should().Be(text.Final.InputHash, "the images stay out of the input hash");
        withImages.Final.ImageCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_ShouldAnswerFromTheCache_OnlyForTheSameImageBytes()
    {
        await PosterAsync(maxImages: 1);
        images.Put("1", seed: 1);
        Caller.Respond = _ => ScriptedAiModelCaller.Answer(Json("close"));

        var first = (await RunAsync(Request(MemoryImages.Ref("1")))).ValueOrThrow();
        var cached = (await RunAsync(Request(MemoryImages.Ref("1")))).ValueOrThrow();
        images.Put("1", seed: 9);
        var changed = (await RunAsync(Request(MemoryImages.Ref("1")))).ValueOrThrow();
        var text = (await RunAsync(Request())).ValueOrThrow();

        cached.FromCache.Should().BeTrue();
        cached.Final.Id.Should().Be(first.Final.Id);
        changed.FromCache.Should().BeFalse("new bytes behind the same key are a new input");
        changed.Final.InputHash.Should().NotBe(first.Final.InputHash);
        text.FromCache.Should().BeFalse("a call with no image is another input");
        Caller.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task Run_ShouldSendImagesOnlyToTheRoutedPromptThatTakesThem()
    {
        await PosterAsync(maxImages: null);
        await SaveAsync(UseCase, "Poster: {{title}}", match: "gpt-6-luna", parameters: new PromptParameters { MaxImages = 1 });
        images.Put("1");
        Caller.Enqueue(ScriptedAiModelCaller.Answer("not json"), ScriptedAiModelCaller.Answer(Json("close")));

        var run = (await RunAsync(Request(MemoryImages.Ref("1")))).ValueOrThrow();

        run.Attempts.Select(attempt => (attempt.Model, attempt.ImageCount)).Should().Equal(("claude-haiku-5-5", 0), ("gpt-6-luna", 1));
        Caller.Requests.Select(request => request.Images.Count).Should().Equal(0, 1);
    }

    [Fact]
    public async Task Run_ShouldRefuseBeforeAnyCall_OverTheLimitsOrWhenAnImageCannotBeRead()
    {
        await PosterAsync(maxImages: 4);
        images.Put("small");
        images.Put("large", new byte[65]);
        images.Put("text", "plain text, not an image"u8.ToArray());

        var tooMany = await RunAsync(Request([.. Enumerable.Range(0, 5).Select(_ => MemoryImages.Ref("small"))]));
        var malformed = await RunAsync(Request(new AiImageRef { Uri = "file:///etc/passwd" }));
        var missing = await RunAsync(Request(MemoryImages.Ref("small"), MemoryImages.Ref("gone")));
        var large = await RunAsync(Request(MemoryImages.Ref("large")));
        var notImage = await RunAsync(Request(MemoryImages.Ref("text")));
        var changed = await RunAsync(Request(MemoryImages.Ref("small") with { Sha256 = new string('0', 64) }));

        Error(tooMany).Message.Should().Be("A call carries at most 4 images, not 5.");
        Error(malformed).Type.Should().Be(AppErrorType.Validation);
        Error(missing).Type.Should().Be(AppErrorType.NotFound);
        Error(missing).Message.Should().Be("The image 'fss:gone' could not be read: no image resolver handles that key.");
        Error(large).Message.Should().Be("The image 'fss:large' is over the limit of 64 bytes.");
        Error(notImage).Message.Should().Be("The image 'fss:text' is not a JPEG, PNG, WebP or GIF image.");
        Error(changed).Message.Should().EndWith("no longer matches its SHA-256.");
        Caller.Requests.Should().BeEmpty();
        (await InferencesAsync(log => log.ListAsync(new InferenceQuery { UseCase = UseCase }))).ValueOrThrow().Should().BeEmpty("a refused call logs nothing");
    }

    [Fact]
    public async Task SavePrompt_ShouldRefuseMaxImagesOutOfRange()
    {
        await PosterAsync(maxImages: null);

        var negative = await PromptsAsync(prompts => prompts.SavePromptAsync(new PromptDraft
        {
            UseCase = UseCase, SystemTemplate = "s", UserTemplate = "u", Parameters = new PromptParameters { MaxImages = -1 },
        }));
        var many = await PromptsAsync(prompts => prompts.SavePromptAsync(new PromptDraft
        {
            UseCase = UseCase, SystemTemplate = "s", UserTemplate = "u", Parameters = new PromptParameters { MaxImages = 21 },
        }));

        negative.Should().BeOfType<Result<PromptModel>.Failure>().Which.Error.Message.Should().Be("The most images a prompt takes must be 0 to 20.");
        many.Should().BeOfType<Result<PromptModel>.Failure>();
    }

    private async Task PosterAsync(int? maxImages)
    {
        await UseCaseAsync(UseCase, user: "Poster: {{title}}");
        await SaveAsync(UseCase, "Poster: {{title}}", parameters: new PromptParameters { MaxImages = maxImages });
    }

    private static AiRunRequest Request(params AiImageRef[] refs) => new() { UseCase = UseCase, Variables = Post("Tashkent Fashion Week"), Images = refs };

    private static AppError Error(Result<AiRunModel> result) => result.Should().BeOfType<Result<AiRunModel>.Failure>().Subject.Error;
}
