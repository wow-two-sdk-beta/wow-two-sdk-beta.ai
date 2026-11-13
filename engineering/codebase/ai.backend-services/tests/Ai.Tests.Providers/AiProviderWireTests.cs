using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Ai.Tests.Providers.Support;
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
