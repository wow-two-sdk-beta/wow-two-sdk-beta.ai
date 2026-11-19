using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Ai.Tests.Providers.Support;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Providers;

/// <summary>The real SDKs against a stub HTTP handler: what each kind puts on the wire and how its answers map back.</summary>
public sealed class AiProviderWireTests
{
    private const string Schema = """{"type":"object","properties":{"label":{"type":"string"}},"required":["label"]}""";

    private static AiCallRequest Request(string provider, string model) => new()
    {
        Model = new AiModelRef(provider, model),
        System = "You sort notes.",
        User = "Sort: milk",
        MaxOutputTokens = 256,
    };

    private const string AnthropicAnswer = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5",
         "content":[{"type":"text","text":"{\"label\":\"food\"}"}],"stop_reason":"end_turn","stop_sequence":null,
         "usage":{"input_tokens":10,"output_tokens":5,"cache_creation_input_tokens":300,"cache_read_input_tokens":200}}
        """;

    [Fact]
    public async Task Anthropic_ShouldSendACacheableSystemBlockAndTheSchema_AndMapTheUsage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicAnswer);

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema });

        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.anthropic.com/v1/messages",
            "an empty BaseUrl takes the public endpoint, never ANTHROPIC_BASE_URL from the environment");
        // Anthropic 13.x sends the key as a bearer token; compared as a flag so no credential reaches the output.
        (handler.LastRequest.Headers.Authorization?.Parameter == Callers.AnthropicKey).Should().BeTrue();
        handler.Calls.Should().Be(1);
        var body = JsonNode.Parse(handler.LastBody!)!;
        body["model"]!.GetValue<string>().Should().Be("claude-haiku-5-5");
        body["max_tokens"]!.GetValue<int>().Should().Be(256);
        body["system"]![0]!["text"]!.GetValue<string>().Should().Be("You sort notes.");
        body["system"]![0]!["cache_control"]!["type"]!.GetValue<string>().Should().Be("ephemeral");
        body["output_config"]!["format"]!["type"]!.GetValue<string>().Should().Be("json_schema");
        body["output_config"]!["format"]!["schema"]!["required"]![0]!.GetValue<string>().Should().Be("label");

        result.Should().Match<AiCallResult>(r => r.Succeeded && r.Text == "{\"label\":\"food\"}" && r.FinishReason == "end_turn"
            && r.InputTokens == 510 && r.CacheReadTokens == 200 && r.CacheWriteTokens == 300 && r.OutputTokens == 5);
    }

    [Theory]
    [InlineData("none", "disabled", null)]
    [InlineData("low", "adaptive", "low")]
    [InlineData("high", "adaptive", "high")]
    public async Task Anthropic_ShouldMapTheEffort(string effort, string thinking, string? outputEffort)
    {
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicAnswer);

        await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5") with { Effort = effort });

        var body = JsonNode.Parse(handler.LastBody!)!;
        body["thinking"]!["type"]!.GetValue<string>().Should().Be(thinking);
        body["output_config"]?["effort"]?.GetValue<string>().Should().Be(outputEffort);
    }

    /// <summary>What Sonnet 5.5 answered on 2026-11-18 to <c>thinking: disabled</c>: a model that always thinks.</summary>
    private const string BetweenToolsRefusal = """
        {"type":"error","error":{"type":"invalid_request_error","message":"To turn thinking off on this model, send \"thinking\": {\"type\": \"between_tools\"} instead of {\"type\": \"disabled\"}. The model does not think before responding."}}
        """;

    private const string OtherBadRequest = """{"type":"error","error":{"type":"invalid_request_error","message":"max_tokens: too large"}}""";

    private static string ThinkingTypeOf(string body) => JsonNode.Parse(body)!["thinking"]?["type"]?.GetValue<string>() ?? "absent";

    /// <summary>Plays models that always think: <paramref name="alwaysThinks"/> refuse <c>disabled</c> with the 400 above,
    /// any other model (Haiku-like) takes whatever it is sent. <paramref name="onRequest"/> sees each request's thinking type.</summary>
    private static StubHandler Models(Func<string, bool> alwaysThinks, Action<string>? onRequest = null) => new(HttpStatusCode.OK, "")
    {
        Respond = body =>
        {
            var type = ThinkingTypeOf(body);
            onRequest?.Invoke(type);
            return type == "disabled" && alwaysThinks(JsonNode.Parse(body)!["model"]!.GetValue<string>())
                ? (HttpStatusCode.BadRequest, BetweenToolsRefusal)
                : (HttpStatusCode.OK, AnthropicAnswer);
        },
    };

    private static AiCallRequest ThinkingOff(string model) => Request("anthropic", model) with { Effort = "none" };

    [Fact]
    public async Task Anthropic_ShouldSendThinkingDisabledInOneRequest_WhenTheModelTakesIt()
    {
        var handler = Models(_ => false);

        var result = await Callers.Wire(handler).CallAsync(ThinkingOff("claude-haiku-5-5"));

        handler.Calls.Should().Be(1);
        ThinkingTypeOf(handler.Bodies[0]).Should().Be("disabled");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Anthropic_ShouldRetryWithBetweenToolsOnce_WhenTheModelRefusesDisabled_AndSendItFirstAfterwards()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHandler(HttpStatusCode.OK, "")
        {
            Respond = body =>
            {
                var refused = ThinkingTypeOf(body) == "disabled";
                time.Advance(TimeSpan.FromMilliseconds(refused ? 40 : 60));
                return refused ? (HttpStatusCode.BadRequest, BetweenToolsRefusal) : (HttpStatusCode.OK, AnthropicAnswer);
            },
        };
        var caller = Callers.Wire(handler, time: time);
        var request = ThinkingOff("claude-sonnet-5-5") with { JsonSchema = Schema };

        var first = await caller.CallAsync(request);

        handler.Calls.Should().Be(2);
        handler.Bodies.Select(ThinkingTypeOf).Should().Equal("disabled", "between_tools");
        var retry = JsonNode.Parse(handler.Bodies[1])!;
        retry["thinking"]!.AsObject().Count.Should().Be(1, "between_tools takes no other field");
        retry["model"]!.GetValue<string>().Should().Be("claude-sonnet-5-5");
        retry["max_tokens"]!.GetValue<int>().Should().Be(256);
        retry["system"]![0]!["cache_control"]!["type"]!.GetValue<string>().Should().Be("ephemeral");
        retry["output_config"]!["format"]!["type"]!.GetValue<string>().Should().Be("json_schema");
        first.Should().Match<AiCallResult>(r => r.Succeeded && r.Text == "{\"label\":\"food\"}" && r.InputTokens == 510
            && r.CacheReadTokens == 200 && r.CacheWriteTokens == 300 && r.OutputTokens == 5);
        first.LatencyMs.Should().Be(100, "both attempts count: 40 ms refused plus 60 ms answered");

        var second = await caller.CallAsync(request);

        handler.Calls.Should().Be(3, "the model is remembered, so the second call needs one request");
        ThinkingTypeOf(handler.Bodies[2]).Should().Be("between_tools");
        second.Should().Match<AiCallResult>(r => r.Succeeded && r.LatencyMs == 60);
    }

    [Fact]
    public async Task Anthropic_ShouldRememberBetweenToolsPerModel()
    {
        var handler = Models(model => model == "claude-sonnet-5-5");
        var caller = Callers.Wire(handler);

        await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));
        await caller.CallAsync(ThinkingOff("claude-haiku-5-5"));

        handler.Bodies.Select(ThinkingTypeOf).Should().Equal("disabled", "between_tools", "disabled");
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task Anthropic_ShouldNeverSendBetweenTools_ForAnotherEffort(string effort)
    {
        // Even a model remembered as between_tools keeps the adaptive mapping; and the 400 that names between_tools is no
        // reason to retry an attempt that never asked for thinking off.
        var handler = Models(_ => true);
        var caller = Callers.Wire(handler);
        await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));
        handler.Bodies.Clear();

        await caller.CallAsync(Request("anthropic", "claude-sonnet-5-5") with { Effort = effort });

        handler.Bodies.Should().ContainSingle();
        var body = JsonNode.Parse(handler.Bodies[0])!;
        body["thinking"]!["type"]!.GetValue<string>().Should().Be("adaptive");
        body["output_config"]!["effort"]!.GetValue<string>().Should().Be(effort);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("high")]
    public async Task Anthropic_ShouldNotRetryAnotherEffort_ThatAnswersTheBetweenToolsRefusal(string effort)
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, BetweenToolsRefusal);

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-sonnet-5-5") with { Effort = effort });

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        handler.Calls.Should().Be(1);
        handler.Bodies.Select(ThinkingTypeOf).Should().NotContain("between_tools");
    }

    [Fact]
    public async Task Anthropic_ShouldNotRetryADifferentBadRequest_AndShouldKeepSendingDisabled()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, OtherBadRequest);
        var caller = Callers.Wire(handler);

        var result = await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));
        await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        result.Error.Should().Contain("max_tokens: too large");
        handler.Bodies.Select(ThinkingTypeOf).Should().Equal("disabled", "disabled");
    }

    [Fact]
    public async Task Anthropic_ShouldRetryOnlyOnce_AndRememberNothing_WhenBetweenToolsFailsToo()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "")
        {
            Respond = body => ThinkingTypeOf(body) == "disabled"
                ? (HttpStatusCode.BadRequest, BetweenToolsRefusal)
                : ((HttpStatusCode)529, """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"""),
        };
        var caller = Callers.Wire(handler);

        var result = await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));
        await caller.CallAsync(ThinkingOff("claude-sonnet-5-5"));

        result.ErrorKind.Should().Be(AiErrorKind.Unavailable);
        handler.Bodies.Select(ThinkingTypeOf).Should().Equal("disabled", "between_tools", "disabled", "between_tools");
    }

    [Fact]
    public async Task OpenAi_ShouldNotRetry_WhenItsBadRequestNamesBetweenTools()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, BetweenToolsRefusal);

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b") with { Effort = "none" });

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        handler.Calls.Should().Be(1);
    }

    /// <summary>What Haiku 5.5 and Sonnet 5.5 answered on 2026-11-19 to SCFD's extraction schemas as structured output.</summary>
    private const string GrammarTooLarge = """
        {"type":"error","error":{"type":"invalid_request_error","message":"The compiled grammar is too large, which would cause performance issues. Simplify your tool schemas or reduce the number of strict tools."}}
        """;

    private const string OtherSchema = """{"type":"object","properties":{"tag":{"type":"string"}},"required":["tag"]}""";

    private static string FormatTypeOf(string body) => JsonNode.Parse(body)!["output_config"]?["format"]?["type"]?.GetValue<string>() ?? "absent";

    private static string SystemTextOf(string body) => JsonNode.Parse(body)!["system"]![0]!["text"]!.GetValue<string>();

    /// <summary>Plays Anthropic refusing every schema sent as structured output with the 400 above; <paramref name="other"/>
    /// answers any other request.</summary>
    private static StubHandler GrammarRefused(Func<(HttpStatusCode, string)>? other = null) => new(HttpStatusCode.OK, "")
    {
        Respond = body => FormatTypeOf(body) == "json_schema"
            ? (HttpStatusCode.BadRequest, GrammarTooLarge)
            : other?.Invoke() ?? (HttpStatusCode.OK, AnthropicAnswer),
    };

    [Fact]
    public async Task Anthropic_ShouldRetryWithTheSchemaInTheInstructionsOnce_WhenTheGrammarIsTooLarge_AndSendItFirstAfterwards()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHandler(HttpStatusCode.OK, "")
        {
            Respond = body =>
            {
                var refused = FormatTypeOf(body) == "json_schema";
                time.Advance(TimeSpan.FromMilliseconds(refused ? 40 : 60));
                return refused ? (HttpStatusCode.BadRequest, GrammarTooLarge) : (HttpStatusCode.OK, AnthropicAnswer);
            },
        };
        var caller = Callers.Wire(handler, time: time);
        var request = Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema, SchemaName = "sort-note" };

        var first = await caller.CallAsync(request);

        handler.Bodies.Select(FormatTypeOf).Should().Equal("json_schema", "absent");
        var retry = JsonNode.Parse(handler.Bodies[1])!;
        retry["output_config"]?["format"].Should().BeNull("Anthropic has no JSON mode, so the retry sends no format");
        SystemTextOf(handler.Bodies[1]).Should().Be($"You sort notes.\n\nAnswer with one JSON object that matches this JSON schema:\n\n{Schema}");
        retry["system"]!.AsArray().Count.Should().Be(1);
        retry["system"]![0]!["cache_control"]!["type"]!.GetValue<string>().Should().Be("ephemeral");
        retry["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("Sort: milk");
        first.Should().Match<AiCallResult>(r => r.Succeeded && r.SchemaInInstructions && r.Text == "{\"label\":\"food\"}"
            && r.InputTokens == 510 && r.OutputTokens == 5);
        first.LatencyMs.Should().Be(100, "both attempts count: 40 ms refused plus 60 ms answered");

        var second = await caller.CallAsync(request);

        handler.Calls.Should().Be(3, "the refused schema is remembered, so the second call needs one request");
        FormatTypeOf(handler.Bodies[2]).Should().Be("absent");
        SystemTextOf(handler.Bodies[2]).Should().Be(SystemTextOf(handler.Bodies[1]));
        second.Should().Match<AiCallResult>(r => r.Succeeded && r.SchemaInInstructions && r.LatencyMs == 60);
    }

    [Fact]
    public async Task Anthropic_ShouldRememberTheRefusedSchemaPerModelAndSchema()
    {
        var handler = GrammarRefused();
        var caller = Callers.Wire(handler);

        await caller.CallAsync(Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema });
        await caller.CallAsync(Request("anthropic", "claude-sonnet-5-5") with { JsonSchema = Schema });
        await caller.CallAsync(Request("anthropic", "claude-haiku-5-5") with { JsonSchema = OtherSchema });
        await caller.CallAsync(Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema });

        handler.Bodies.Select(FormatTypeOf).Should().Equal(
            "json_schema", "absent",
            "json_schema", "absent",
            "json_schema", "absent",
            "absent");
    }

    [Fact]
    public async Task Anthropic_ShouldNotRetryADifferentBadRequest_AndShouldKeepSendingTheSchema()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, OtherBadRequest);
        var caller = Callers.Wire(handler);
        var request = Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema };

        var result = await caller.CallAsync(request);
        await caller.CallAsync(request);

        result.Should().Match<AiCallResult>(r => r.ErrorKind == AiErrorKind.BadRequest && !r.SchemaInInstructions);
        result.Error.Should().Contain("max_tokens: too large");
        handler.Bodies.Select(FormatTypeOf).Should().Equal("json_schema", "json_schema");
    }

    [Fact]
    public async Task Anthropic_ShouldNotRetryTheGrammarRefusal_ForACallWithoutASchema()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, GrammarTooLarge);

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5"));

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Anthropic_ShouldRetryOnlyOnce_AndStillRememberTheSchema_WhenTheInstructionsAttemptFails()
    {
        var handler = GrammarRefused(() => ((HttpStatusCode)529, """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"""));
        var caller = Callers.Wire(handler);
        var request = Request("anthropic", "claude-haiku-5-5") with { JsonSchema = Schema };

        var result = await caller.CallAsync(request);
        var again = await caller.CallAsync(request);

        result.Should().Match<AiCallResult>(r => r.ErrorKind == AiErrorKind.Unavailable && r.SchemaInInstructions);
        again.ErrorKind.Should().Be(AiErrorKind.Unavailable);
        handler.Bodies.Select(FormatTypeOf).Should().Equal("json_schema", "absent", "absent");
    }

    [Fact]
    public async Task Anthropic_ShouldTakeBothRetries_WhenThinkingOffAndTheSchemaAreRefused()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "")
        {
            Respond = body => ThinkingTypeOf(body) == "disabled" ? (HttpStatusCode.BadRequest, BetweenToolsRefusal)
                : FormatTypeOf(body) == "json_schema" ? (HttpStatusCode.BadRequest, GrammarTooLarge)
                : (HttpStatusCode.OK, AnthropicAnswer),
        };
        var caller = Callers.Wire(handler);
        var request = ThinkingOff("claude-sonnet-5-5") with { JsonSchema = Schema };

        var first = await caller.CallAsync(request);
        await caller.CallAsync(request);

        handler.Bodies.Select(body => $"{ThinkingTypeOf(body)} {FormatTypeOf(body)}").Should().Equal(
            "disabled json_schema", "between_tools json_schema", "between_tools absent",
            "between_tools absent");
        first.Should().Match<AiCallResult>(r => r.Succeeded && r.SchemaInInstructions);
    }

    [Fact]
    public async Task OpenAi_ShouldNotRetry_OnTheAnthropicGrammarWording()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, GrammarTooLarge);

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b") with { JsonSchema = Schema });

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("groq", "openai", "openai/gpt-oss-20b")]
    [InlineData("ollama", "ollama", "llama3.2")]
    public async Task SchemaInInstructions_ShouldSendTheKindsJsonMode_WhereItHasOne(string provider, string kind, string model)
    {
        var handler = new StubHandler(HttpStatusCode.OK, kind == "openai" ? OpenAiAnswer : OllamaAnswer.ReplaceLineEndings(" "));
        var providers = Callers.Providers();
        using var factory = new WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal.AiInnerChatClientFactory(() => handler);
        var client = factory.Create(provider, kind, providers.Providers[provider], model);
        var request = Request(provider, model) with { JsonSchema = Schema };
        WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal.AiModelCaller.TryBuild(request, kind, out var messages, out var options, out _).Should().BeTrue();

        WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal.AiModelCaller.UseSchemaInInstructions(messages, options, request, kind);
        await client.GetResponseAsync(messages, options);

        var body = JsonNode.Parse(handler.LastBody!)!;
        if (kind == "openai")
            body["response_format"]!["type"]!.GetValue<string>().Should().Be("json_object");
        else
            body["format"]!.GetValue<string>().Should().Be("json");
        body["messages"]![0]!["role"]!.GetValue<string>().Should().Be("system");
        body["messages"]![0]!["content"]!.GetValue<string>().Should().EndWith(Schema).And.StartWith("You sort notes.");
    }

    [Theory]
    [InlineData(529, """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""", AiErrorKind.Unavailable)]
    [InlineData(401, """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""", AiErrorKind.Unauthorized)]
    [InlineData(400, """{"type":"error","error":{"type":"invalid_request_error","message":"max_tokens: too large"}}""", AiErrorKind.BadRequest)]
    public async Task Anthropic_ShouldClassifyAFailedStatus_InOneAttempt(int status, string body, AiErrorKind kind)
    {
        var handler = new StubHandler((HttpStatusCode)status, body);

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5"));

        result.ErrorKind.Should().Be(kind);
        result.Error.Should().Contain(status.ToString()).And.NotContain(Callers.AnthropicKey);
        handler.Calls.Should().Be(1, "the SDK's own retries are off; the module retries");
    }

    [Fact]
    public async Task Anthropic_ShouldAnswerContentFiltered_OnARefusal()
    {
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicAnswer.Replace("\"end_turn\"", "\"refusal\""));

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5"));

        result.Should().Match<AiCallResult>(r => r.ErrorKind == AiErrorKind.ContentFiltered && r.Text == null && r.FinishReason == "refusal");
    }

    [Fact]
    public async Task Anthropic_ShouldAnswerNetwork_WhenTheHostIsUnreachable()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "") { Throws = new HttpRequestException("Connection refused (api.anthropic.com:443)") };

        var result = await Callers.Wire(handler).CallAsync(Request("anthropic", "claude-haiku-5-5"));

        result.ErrorKind.Should().Be(AiErrorKind.Network);
    }

    /// <summary>A tiny PNG's first bytes: enough for every SDK to send, never decoded.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16];

    private static AiCallRequest WithImages(AiCallRequest request) =>
        request with { Images = [new AiImageContent(Png, "image/png"), new AiImageContent(Jpeg, "image/jpeg")] };

    [Fact]
    public async Task Anthropic_ShouldSendBase64ImageBlocksBeforeTheText_AndCountImageTokensAsInput()
    {
        // Anthropic bills an image's tokens as input tokens, so usage needs no image-specific mapping.
        var handler = new StubHandler(HttpStatusCode.OK, AnthropicAnswer.Replace("\"input_tokens\":10", "\"input_tokens\":1610", StringComparison.Ordinal));

        var result = await Callers.Wire(handler).CallAsync(WithImages(Request("anthropic", "claude-haiku-5-5")));

        var content = JsonNode.Parse(handler.LastBody!)!["messages"]![0]!["content"]!.AsArray();
        content.Select(block => block!["type"]!.GetValue<string>()).Should().Equal("image", "image", "text");
        content[0]!["source"]!["type"]!.GetValue<string>().Should().Be("base64");
        content[0]!["source"]!["media_type"]!.GetValue<string>().Should().Be("image/png");
        Convert.FromBase64String(content[0]!["source"]!["data"]!.GetValue<string>()).Should().Equal(Png);
        content[1]!["source"]!["media_type"]!.GetValue<string>().Should().Be("image/jpeg");
        Convert.FromBase64String(content[1]!["source"]!["data"]!.GetValue<string>()).Should().Equal(Jpeg);
        content[2]!["text"]!.GetValue<string>().Should().Be("Sort: milk");
        result.InputTokens.Should().Be(2110, "input, cache reads and cache writes, image tokens inside the input");
    }

    [Fact]
    public async Task OpenAi_ShouldSendDataUrlImageParts_AndCountImageTokensAsPromptTokens()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OpenAiAnswer.Replace("\"prompt_tokens\":50", "\"prompt_tokens\":900", StringComparison.Ordinal));

        var result = await Callers.Wire(handler).CallAsync(WithImages(Request("groq", "openai/gpt-oss-20b")));

        var user = JsonNode.Parse(handler.LastBody!)!["messages"]!.AsArray().Single(message => message!["role"]!.GetValue<string>() == "user")!;
        var parts = user["content"]!.AsArray();
        parts.Select(part => part!["type"]!.GetValue<string>()).Should().Equal("image_url", "image_url", "text");
        parts[0]!["image_url"]!["url"]!.GetValue<string>().Should().Be("data:image/png;base64," + Convert.ToBase64String(Png));
        parts[1]!["image_url"]!["url"]!.GetValue<string>().Should().Be("data:image/jpeg;base64," + Convert.ToBase64String(Jpeg));
        parts[2]!["text"]!.GetValue<string>().Should().Be("Sort: milk");
        result.InputTokens.Should().Be(900);
    }

    [Fact]
    public async Task Ollama_ShouldSendTheImagesAsBase64OnTheUserMessage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OllamaAnswer.ReplaceLineEndings(" ").Replace("\"prompt_eval_count\":12", "\"prompt_eval_count\":700", StringComparison.Ordinal));

        var result = await Callers.Wire(handler).CallAsync(WithImages(Request("ollama", "llama3.2")));

        var user = JsonNode.Parse(handler.LastBody!)!["messages"]!.AsArray().Single(message => message!["role"]!.GetValue<string>() == "user")!;
        user["content"]!.GetValue<string>().Should().Be("Sort: milk");
        user["images"]!.AsArray().Select(image => image!.GetValue<string>()).Should().Equal(Convert.ToBase64String(Png), Convert.ToBase64String(Jpeg));
        result.InputTokens.Should().Be(700);
    }

    [Fact]
    public async Task EveryKind_ShouldSendNoImageBlock_ForATextCall()
    {
        var anthropic = new StubHandler(HttpStatusCode.OK, AnthropicAnswer);
        var openAi = new StubHandler(HttpStatusCode.OK, OpenAiAnswer);
        var ollama = new StubHandler(HttpStatusCode.OK, OllamaAnswer.ReplaceLineEndings(" "));

        await Callers.Wire(anthropic).CallAsync(Request("anthropic", "claude-haiku-5-5"));
        await Callers.Wire(openAi).CallAsync(Request("groq", "openai/gpt-oss-20b"));
        await Callers.Wire(ollama).CallAsync(Request("ollama", "llama3.2"));

        anthropic.LastBody.Should().NotContain("\"image\"");
        openAi.LastBody.Should().NotContain("image_url");
        ollama.LastBody.Should().NotContain("\"images\"");
    }

    private const string OpenAiAnswer = """
        {"id":"c1","object":"chat.completion","created":1,"model":"openai/gpt-oss-20b",
         "choices":[{"index":0,"message":{"role":"assistant","content":"{\"label\":\"food\"}"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":50,"completion_tokens":7,"total_tokens":57,
                  "prompt_tokens_details":{"cached_tokens":20},"completion_tokens_details":{"reasoning_tokens":3}}}
        """;

    [Fact]
    public async Task OpenAi_ShouldCallTheBaseUrlWithTheSchema_AndMapTheUsage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OpenAiAnswer);

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b") with
        {
            JsonSchema = Schema,
            SchemaName = "sort-note",
            Effort = "low",
            Temperature = 0,
        });

        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.groq.com/openai/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {Callers.OpenAiKey}");
        var body = JsonNode.Parse(handler.LastBody!)!;
        body["model"]!.GetValue<string>().Should().Be("openai/gpt-oss-20b");
        body["messages"]![0]!["role"]!.GetValue<string>().Should().Be("system");
        body["messages"]![0]!.ToJsonString().Should().NotContain("cache_control");
        body["response_format"]!["type"]!.GetValue<string>().Should().Be("json_schema");
        body["response_format"]!["json_schema"]!["name"]!.GetValue<string>().Should().Be("sort-note");
        body["reasoning_effort"]!.GetValue<string>().Should().Be("low");
        body["temperature"]!.GetValue<double>().Should().Be(0);

        result.Should().Match<AiCallResult>(r => r.Succeeded && r.FinishReason == "stop" && r.InputTokens == 50
            && r.CacheReadTokens == 20 && r.CacheWriteTokens == 0 && r.OutputTokens == 7 && r.ReasoningTokens == 3);
    }

    [Fact]
    public async Task OpenAi_ShouldAnswerContentFiltered_OnAFilteredChoice()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OpenAiAnswer.Replace("\"stop\"", "\"content_filter\""));

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b"));

        result.Should().Match<AiCallResult>(r => r.ErrorKind == AiErrorKind.ContentFiltered && r.Text == null && r.FinishReason == "content_filter");
    }

    [Theory]
    [InlineData(429, AiErrorKind.RateLimited)]
    [InlineData(503, AiErrorKind.Unavailable)]
    [InlineData(401, AiErrorKind.Unauthorized)]
    [InlineData(400, AiErrorKind.BadRequest)]
    public async Task OpenAi_ShouldClassifyAFailedStatus_InOneAttempt(int status, AiErrorKind kind)
    {
        var handler = new StubHandler((HttpStatusCode)status, """{"error":{"message":"nope","type":"x"}}""");

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b"));

        result.ErrorKind.Should().Be(kind);
        result.Error.Should().NotContain(Callers.OpenAiKey);
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task OpenAi_ShouldAnswerNetwork_WhenTheHostIsUnreachable()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "") { Throws = new HttpRequestException("Connection refused") };

        var result = await Callers.Wire(handler).CallAsync(Request("groq", "openai/gpt-oss-20b"));

        result.ErrorKind.Should().Be(AiErrorKind.Network);
    }

    private const string OllamaAnswer = """
        {"model":"llama3.2","created_at":"2026-11-13T00:00:00Z","message":{"role":"assistant","content":"{\"label\":\"food\"}"},
         "done":true,"done_reason":"stop","prompt_eval_count":12,"eval_count":4}
        """;

    [Fact]
    public async Task Ollama_ShouldCallTheDefaultHostWithoutAKey_AndMapTheUsage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OllamaAnswer.ReplaceLineEndings(" "));

        var result = await Callers.Wire(handler).CallAsync(Request("ollama", "llama3.2") with { JsonSchema = Schema, Effort = "none" });

        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://localhost:11434/api/chat");
        handler.LastRequest.Headers.Authorization.Should().BeNull();
        var body = JsonNode.Parse(handler.LastBody!)!;
        body["model"]!.GetValue<string>().Should().Be("llama3.2");
        body["format"]!["required"]![0]!.GetValue<string>().Should().Be("label");
        body["think"]!.GetValue<bool>().Should().BeFalse();
        body["options"]!["num_predict"]!.GetValue<int>().Should().Be(256);

        result.Should().Match<AiCallResult>(r => r.Succeeded && r.Text == "{\"label\":\"food\"}" && r.FinishReason == "stop"
            && r.InputTokens == 12 && r.OutputTokens == 4);
    }

    [Fact]
    public async Task Ollama_ShouldAnswerNetwork_WhenTheHostIsDown()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "") { Throws = new HttpRequestException("Connection refused (localhost:11434)") };

        var result = await Callers.Wire(handler).CallAsync(Request("ollama", "llama3.2"));

        result.ErrorKind.Should().Be(AiErrorKind.Network);
    }
}
