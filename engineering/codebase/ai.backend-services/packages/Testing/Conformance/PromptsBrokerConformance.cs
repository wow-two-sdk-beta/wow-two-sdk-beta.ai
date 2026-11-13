using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Ai.Testing.Conformance;

/// <summary>The conformance suite of <see cref="IPromptsBroker"/>: rewrite and versioned saves on one table, labels, and
/// the resolution rules every implementation keeps.</summary>
/// <remarks>Derive a test class per implementation and supply the broker. Every test works on a use case of its own, so
/// the suite runs against a shared store.</remarks>
public abstract class PromptsBrokerConformance
{
    /// <summary>Gets the implementation under test.</summary>
    protected abstract IPromptsBroker Broker { get; }

    /// <summary>A use case reads back as it was put, its route normalized.</summary>
    [Fact]
    public async Task PutUseCase_ShouldStoreTheUseCase_AndReadItBack()
    {
        var key = NewKey();

        var put = Value(await Broker.PutUseCaseAsync(new UseCaseModel
        {
            Key = key,
            Description = "Relevance of a post",
            PromptMode = PromptMode.Versioned,
            OutputSchema = """{"type":"object","required":["relevance"]}""",
            Route = ["Anthropic/claude-haiku-5-5", "openai/gpt-6-luna"],
            KeepOutputs = false,
        }));
        var read = Value(await Broker.GetUseCaseAsync(key));

        read.Should().BeEquivalentTo(put);
        read.PromptMode.Should().Be(PromptMode.Versioned);
        read.Route.Should().Equal("anthropic/claude-haiku-5-5", "openai/gpt-6-luna");
        read.KeepOutputs.Should().BeFalse();
        read.OutputSchema.Should().Contain("\"relevance\"");
    }

    /// <summary>A key outside lowercase letters, digits and hyphens is refused.</summary>
    /// <param name="key">A bad key.</param>
    [Theory]
    [InlineData("Upper")]
    [InlineData("with space")]
    [InlineData("")]
    public async Task PutUseCase_ShouldFailValidation_WhenTheKeyIsMalformed(string key)
    {
        var result = await Broker.PutUseCaseAsync(new UseCaseModel { Key = key });

        Error(result).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>A route entry that is not a model reference is refused.</summary>
    [Fact]
    public async Task PutUseCase_ShouldFailValidation_WhenARouteEntryIsNotAModelReference()
    {
        var result = await Broker.PutUseCaseAsync(new UseCaseModel { Key = NewKey(), Route = ["claude-haiku-5-5"] });

        Error(result).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>An unknown use case is not found.</summary>
    [Fact]
    public async Task GetUseCase_ShouldFailNotFound_WhenTheUseCaseDoesNotExist()
    {
        Error(await Broker.GetUseCaseAsync(NewKey())).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>Under rewrite mode a save updates the row for its model match in place: version 1 stays, the text and its
    /// hash change, the creation is kept.</summary>
    [Fact]
    public async Task SavePrompt_ShouldRewriteTheRowInPlace_UnderRewriteMode()
    {
        var key = await UseCaseAsync(PromptMode.Rewrite);

        var first = Value(await Broker.SavePromptAsync(Draft(key, "first {{text}}", by: "max")));
        var second = Value(await Broker.SavePromptAsync(Draft(key, "second {{text}}", by: "claude")));

        second.Id.Should().Be(first.Id);
        second.Version.Should().Be(1);
        second.UserTemplate.Should().Be("second {{text}}");
        second.ContentHash.Should().NotBe(first.ContentHash).And.MatchRegex("^[0-9a-f]{12}$");
        second.CreatedBy.Should().Be("max", "the creator is who saved it first");
        second.UpdatedAtUtc.Should().BeOnOrAfter(first.UpdatedAtUtc);
        Value(await Broker.ListPromptsAsync(key)).Should().ContainSingle().Which.UserTemplate.Should().Be("second {{text}}");
    }

    /// <summary>Under rewrite mode each model match keeps a row of its own, each at version 1.</summary>
    [Fact]
    public async Task SavePrompt_ShouldKeepOneRowPerModelMatch_UnderRewriteMode()
    {
        var key = await UseCaseAsync(PromptMode.Rewrite);

        await Broker.SavePromptAsync(Draft(key, "generic"));
        await Broker.SavePromptAsync(Draft(key, "claude", match: "claude-*"));
        await Broker.SavePromptAsync(Draft(key, "claude again", match: "claude-*"));

        var rows = Value(await Broker.ListPromptsAsync(key));
        rows.Should().HaveCount(2).And.OnlyContain(row => row.Version == 1);
        rows.Select(row => row.UserTemplate).Should().BeEquivalentTo("generic", "claude again");
    }

    /// <summary>Under versioned mode every save inserts the use case's next version and never changes an earlier row.</summary>
    [Fact]
    public async Task SavePrompt_ShouldInsertTheNextVersion_UnderVersionedMode()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);

        var first = Value(await Broker.SavePromptAsync(Draft(key, "v1")));
        await Broker.SavePromptAsync(Draft(key, "v2", match: "gpt-6-luna"));
        var third = Value(await Broker.SavePromptAsync(Draft(key, "v3")));

        third.Version.Should().Be(3, "the next version counts every row of the use case, whatever its match");
        var rows = Value(await Broker.ListPromptsAsync(key));
        rows.Select(row => row.Version).Should().Equal(3, 2, 1);
        rows.Single(row => row.Version == 1).Should().BeEquivalentTo(first, "a versioned row is never updated");
    }

    /// <summary>The same text and parameters hash the same; a parameter change moves the hash.</summary>
    [Fact]
    public async Task SavePrompt_ShouldHashTheTextAndParameters()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);

        var one = Value(await Broker.SavePromptAsync(Draft(key, "same")));
        var two = Value(await Broker.SavePromptAsync(Draft(key, "same")));
        var three = Value(await Broker.SavePromptAsync(Draft(key, "same") with { Parameters = new PromptParameters { Temperature = 0.2 } }));

        two.ContentHash.Should().Be(one.ContentHash);
        three.ContentHash.Should().NotBe(one.ContentHash);
    }

    /// <summary>A prompt for an unknown use case is not found; a malformed match or parameter is refused.</summary>
    [Fact]
    public async Task SavePrompt_ShouldRefuse_AnUnknownUseCaseOrABadDraft()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);

        Error(await Broker.SavePromptAsync(Draft(NewKey(), "x"))).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.SavePromptAsync(Draft(key, "x", match: "*claude"))).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.SavePromptAsync(Draft(key, "x") with { Parameters = new PromptParameters { Effort = "extreme" } })).Type.Should().Be(AppErrorType.Validation);
        Error(await Broker.SavePromptAsync(Draft(key, " "))).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>A label moves to an existing version and lists where it points; a missing version or a rewrite use case is
    /// refused.</summary>
    [Fact]
    public async Task MoveLabel_ShouldPointTheLabel_AtAnExistingVersion()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);
        await Broker.SavePromptAsync(Draft(key, "v1"));
        await Broker.SavePromptAsync(Draft(key, "v2"));

        Value(await Broker.MoveLabelAsync(key, AiConstants.ProductionLabel, 2, "max"));
        var moved = Value(await Broker.MoveLabelAsync(key, AiConstants.ProductionLabel, 1, "claude"));

        moved.Version.Should().Be(1);
        moved.MovedBy.Should().Be("claude");
        Value(await Broker.ListLabelsAsync(key)).Should().ContainSingle().Which.Version.Should().Be(1);
        Error(await Broker.MoveLabelAsync(key, AiConstants.CandidateLabel, 9)).Type.Should().Be(AppErrorType.Validation);

        var rewrite = await UseCaseAsync(PromptMode.Rewrite);
        await Broker.SavePromptAsync(Draft(rewrite, "only"));
        Error(await Broker.MoveLabelAsync(rewrite, AiConstants.ProductionLabel, 1)).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>The target version is the explicit one, else the named label's, else production's, else the newest.</summary>
    [Fact]
    public async Task Resolve_ShouldTargetVersionThenLabelThenProductionThenNewest()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);
        for (var version = 1; version <= 3; version++)
            await Broker.SavePromptAsync(Draft(key, $"v{version}"));

        (await ResolveVersionAsync(key)).Should().Be(3, "with no label the newest version serves");

        await Broker.MoveLabelAsync(key, AiConstants.ProductionLabel, 1);
        await Broker.MoveLabelAsync(key, AiConstants.CandidateLabel, 2);

        (await ResolveVersionAsync(key)).Should().Be(1, "production serves by default");
        (await ResolveVersionAsync(key, label: AiConstants.CandidateLabel)).Should().Be(2);
        (await ResolveVersionAsync(key, version: 3)).Should().Be(3);
        (await ResolveVersionAsync(key, version: 3, label: AiConstants.CandidateLabel)).Should().Be(3, "an explicit version wins over a label");
        Error(await Broker.ResolveAsync(key, "claude-haiku-5-5", label: "missing")).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>Among rows at or below the target, the most specific match wins (exact, then the longest prefix, then
    /// none), then the highest version.</summary>
    [Fact]
    public async Task Resolve_ShouldPickTheMostSpecificMatch_ThenTheHighestVersion()
    {
        var key = await UseCaseAsync(PromptMode.Versioned);
        await Broker.SavePromptAsync(Draft(key, "exact haiku", match: "claude-haiku-5-5"));   // v1
        await Broker.SavePromptAsync(Draft(key, "claude family", match: "claude-*"));        // v2
        await Broker.SavePromptAsync(Draft(key, "haiku family", match: "claude-haiku-*"));   // v3
        await Broker.SavePromptAsync(Draft(key, "generic"));                                 // v4
        await Broker.SavePromptAsync(Draft(key, "generic newer"));                           // v5

        (await ResolveTextAsync(key, "claude-haiku-5-5")).Should().Be("exact haiku");
        (await ResolveTextAsync(key, "claude-haiku-6")).Should().Be("haiku family", "the longest prefix wins");
        (await ResolveTextAsync(key, "claude-sonnet-5-5")).Should().Be("claude family");
        (await ResolveTextAsync(key, "gpt-6-luna")).Should().Be("generic newer", "among equal matches the highest version wins");
        (await ResolveTextAsync(key, "gpt-6-luna", version: 4)).Should().Be("generic", "rows above the target never serve");
        (await ResolveTextAsync(key, "claude-haiku-5-5", version: 2)).Should().Be("exact haiku");
    }

    /// <summary>A model no row fits, or a use case with no prompt, is not found.</summary>
    [Fact]
    public async Task Resolve_ShouldFailNotFound_WhenNoRowFits()
    {
        var key = await UseCaseAsync(PromptMode.Rewrite);

        Error(await Broker.ResolveAsync(key, "claude-haiku-5-5")).Type.Should().Be(AppErrorType.NotFound);

        await Broker.SavePromptAsync(Draft(key, "claude only", match: "claude-*"));
        Error(await Broker.ResolveAsync(key, "gpt-6-luna")).Type.Should().Be(AppErrorType.NotFound);
        Error(await Broker.ResolveAsync(NewKey(), "gpt-6-luna")).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>Creates a use case of the given mode with a fresh key.</summary>
    /// <param name="mode">How it keeps prompts.</param>
    /// <returns>Its key.</returns>
    protected async Task<string> UseCaseAsync(PromptMode mode)
    {
        var key = NewKey();
        Value(await Broker.PutUseCaseAsync(new UseCaseModel { Key = key, PromptMode = mode, Route = ["anthropic/claude-haiku-5-5"] }));
        return key;
    }

    /// <summary>Creates a fresh use case key.</summary>
    protected static string NewKey() => $"conf-{Guid.NewGuid():N}"[..29];

    /// <summary>Builds a draft.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="user">The user template.</param>
    /// <param name="match">The model match.</param>
    /// <param name="by">Who saves it.</param>
    protected static PromptDraft Draft(string useCase, string user, string? match = null, string? by = null) => new()
    {
        UseCase = useCase,
        ModelMatch = match,
        SystemTemplate = "You classify posts.",
        UserTemplate = user,
        By = by,
    };

    private async Task<int> ResolveVersionAsync(string key, int? version = null, string? label = null) =>
        Value(await Broker.ResolveAsync(key, "claude-haiku-5-5", version, label)).Version;

    private async Task<string> ResolveTextAsync(string key, string model, int? version = null) =>
        Value(await Broker.ResolveAsync(key, model, version)).UserTemplate;

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
