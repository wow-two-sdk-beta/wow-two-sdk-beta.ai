using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Unit.Shared;

public sealed class AiHashesTests
{
    [Fact]
    public void Content_ShouldBeTwelveLowercaseHex_AndDeterministic()
    {
        var parameters = new PromptParameters { Temperature = 0.2, MaxOutputTokens = 256, Effort = "low" };

        var first = AiHashes.Content("system", "user {{x}}", parameters);
        var second = AiHashes.Content("system", "user {{x}}", parameters with { });

        first.Should().MatchRegex("^[0-9a-f]{12}$").And.Be(second);
        first.Should().Be(AiHashes.Sha256("system\u001fuser {{x}}\u001f{\"effort\":\"low\",\"maxOutputTokens\":256,\"temperature\":0.2}")[..12]);
    }

    [Fact]
    public void Content_ShouldChange_WhenATemplateOrAParameterChanges()
    {
        var baseline = AiHashes.Content("system", "user", new PromptParameters());

        AiHashes.Content("system", "user ", new PromptParameters()).Should().NotBe(baseline);
        AiHashes.Content("system", "user", new PromptParameters { Temperature = 0 }).Should().NotBe(baseline);
        AiHashes.Content("systemuser", "", new PromptParameters()).Should().NotBe(AiHashes.Content("system", "user", new PromptParameters()),
            "the separator keeps text moved between the templates apart");
    }

    [Fact]
    public void CanonicalParameters_ShouldOrderKeys_AndLeaveNullsOut()
    {
        AiJson.CanonicalParameters(new PromptParameters()).Should().Be("{}");
        AiJson.CanonicalParameters(new PromptParameters { Temperature = 1, MaxOutputTokens = 10 }).Should().Be("""{"maxOutputTokens":10,"temperature":1}""");
    }

    [Fact]
    public void Input_ShouldCoverTheRenderedMessagesSchemaModelAndParameters()
    {
        var parameters = new PromptParameters { MaxOutputTokens = 1024 };
        var baseline = AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters);

        baseline.Should().MatchRegex("^[0-9a-f]{64}$");
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters).Should().Be(baseline);
        AiHashes.Input("s", "u2", "{}", "anthropic/claude-haiku-5-5", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", null, "anthropic/claude-haiku-5-5", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", "{}", "openai/gpt-6-luna", parameters).Should().NotBe(baseline);
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters with { MaxOutputTokens = 512 }).Should().NotBe(baseline);
    }

    [Fact]
    public void Input_ShouldStayTheTextHash_WithNoImage_AndCoverEachImageInOrder()
    {
        var parameters = new PromptParameters { MaxOutputTokens = 1024, MaxImages = 2 };
        var text = AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters);
        string[] images = [new string('a', 64), new string('b', 64)];

        text.Should().Be(AiHashes.Sha256("s\u001fu\u001f{}\u001fanthropic/claude-haiku-5-5\u001f{\"maxImages\":2,\"maxOutputTokens\":1024}"),
            "a call that sends no image hashes as it always did");
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters, []).Should().Be(text);
        var withImages = AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters, images);
        withImages.Should().NotBe(text);
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters, [.. images.Reverse()]).Should().NotBe(withImages, "the order is part of the input");
        AiHashes.Input("s", "u", "{}", "anthropic/claude-haiku-5-5", parameters, [images[0]]).Should().NotBe(withImages);
    }

    [Fact]
    public void Content_ShouldKeepEveryTextOnlyPromptsHash_AndCountMaxImagesAboveZero()
    {
        var baseline = AiHashes.Content("system", "user", new PromptParameters { Temperature = 0 });

        AiHashes.Content("system", "user", new PromptParameters { Temperature = 0, MaxImages = 0 }).Should().Be(baseline, "0 takes text only, as none does");
        AiHashes.Content("system", "user", new PromptParameters { Temperature = 0, MaxImages = 1 }).Should().NotBe(baseline);
        AiJson.CanonicalParameters(new PromptParameters { Effort = "low", MaxImages = 3, MaxOutputTokens = 10 })
            .Should().Be("""{"effort":"low","maxImages":3,"maxOutputTokens":10}""");
    }

    [Fact]
    public void ItemInputHash_ShouldStayAsItWas_WithNoImage_AndCoverTheReferences()
    {
        var input = new Dictionary<string, string?> { ["title"] = "Fashion week" };

        var text = ModuleEvalsBroker.InputHash(input);

        text.Should().Be(AiHashes.Sha256("""{"title":"Fashion week"}"""));
        ModuleEvalsBroker.InputHash(input, []).Should().Be(text);
        var keyed = ModuleEvalsBroker.InputHash(input, [new AiImageRef { Key = "fss:1" }]);
        keyed.Should().NotBe(text);
        ModuleEvalsBroker.InputHash(input, [new AiImageRef { Key = "fss:2" }]).Should().NotBe(keyed);
    }

    [Fact]
    public void Images_ShouldRoundTripAsStoredJson_WithoutNulls()
    {
        IReadOnlyList<AiImageRef> images = [new AiImageRef { Key = "fss:1", MediaType = "image/png" }, new AiImageRef { Uri = "https://a.test/x.jpg" }];

        var json = AiJson.WriteImages(images);

        json.Should().Be("""[{"key":"fss:1","mediaType":"image/png"},{"uri":"https://a.test/x.jpg"}]""");
        AiJson.ReadImages(json).Should().Equal(images);
        AiJson.ReadImages("[]").Should().BeEmpty();
        AiJson.ReadImages(null).Should().BeEmpty();
    }
}
