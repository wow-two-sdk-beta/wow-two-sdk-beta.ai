using System.Text.Json;
using Anthropic.Models.Messages;
using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using Ai.Tests.Providers.Support;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Providers;

/// <summary>The caller's request and result mapping, through a fake inner client.</summary>
public sealed class AiModelCallerTests
{
    private static AiCallRequest Request(string model = "anthropic/claude-haiku-5-5") => new()
    {
        Model = AiModelRef.TryParse(model, out var reference) ? reference! : throw new ArgumentException(model),
        System = "You sort notes.",
        User = "Sort: milk",
    };

    [Fact]
    public async Task CallAsync_ShouldSendTheSystemAndUserMessagesAndTheOptions()
    {
        var client = new FakeChatClient();

        await Callers.Fake(client).CallAsync(Request() with
        {
            JsonSchema = """{"type":"object","properties":{"label":{"type":"string"}},"required":["label"]}""",
            SchemaName = "sort-note",
            Temperature = 0.2,
            MaxOutputTokens = 300,
            Effort = "Low",
        });

        client.Messages.Select(message => (message.Role, message.Text)).Should().Equal(
            (ChatRole.System, "You sort notes."), (ChatRole.User, "Sort: milk"));
        client.Options!.ModelId.Should().Be("claude-haiku-5-5");
        client.Options.MaxOutputTokens.Should().Be(300);
        client.Options.Temperature.Should().BeApproximately(0.2f, 0.0001f);
        client.Options.Reasoning!.Effort.Should().Be(ReasoningEffort.Low);
        var format = client.Options.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>().Subject;
        format.SchemaName.Should().Be("sort-note");
        format.Schema!.Value.GetProperty("required")[0].GetString().Should().Be("label");
    }

    [Fact]
    public async Task CallAsync_ShouldLeaveOptionalOptionsUnset_AndSkipAnEmptySystemMessage()
    {
        var client = new FakeChatClient();

        await Callers.Fake(client).CallAsync(Request() with { System = " " });

        client.Messages.Should().ContainSingle().Which.Role.Should().Be(ChatRole.User);
        client.Options!.Temperature.Should().BeNull();
        client.Options.Reasoning.Should().BeNull();
        client.Options.ResponseFormat.Should().BeNull();
        client.Options.MaxOutputTokens.Should().Be(1024);
    }

    [Theory]
    [InlineData("none", ReasoningEffort.None)]
    [InlineData("medium", ReasoningEffort.Medium)]
    [InlineData("HIGH", ReasoningEffort.High)]
    public async Task CallAsync_ShouldMapTheEffort(string effort, ReasoningEffort expected)
    {
        var client = new FakeChatClient();

        await Callers.Fake(client).CallAsync(Request() with { Effort = effort });

        client.Options!.Reasoning!.Effort.Should().Be(expected);
    }

    [Theory]
    [InlineData("anthropic/claude-haiku-5-5", true, true)]
    [InlineData("anthropic/claude-haiku-5-5", false, false)]
    [InlineData("groq/openai/gpt-oss-20b", true, false)]
    public async Task CallAsync_ShouldMarkTheSystemMessageCacheable_OnlyOnAnthropic(string model, bool cacheSystem, bool marked)
    {
        var client = new FakeChatClient();

        await Callers.Fake(client).CallAsync(Request(model) with { CacheSystem = cacheSystem });

        var system = client.Messages[0].Contents.Should().ContainSingle().Subject;
        (system.AdditionalProperties?.ContainsKey("anthropic:cache_control") ?? false).Should().Be(marked);
    }

    [Theory]
    [InlineData("{not json", null, "does not parse")]
    [InlineData(null, "extreme", "not none, low, medium or high")]
    public async Task CallAsync_ShouldAnswerBadRequest_WithoutACall_WhenTheRequestIsInvalid(string? schema, string? effort, string error)
    {
        var client = new FakeChatClient();

        var result = await Callers.Fake(client).CallAsync(Request() with { JsonSchema = schema, Effort = effort });

        result.ErrorKind.Should().Be(AiErrorKind.BadRequest);
        result.Error.Should().Contain(error);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CallAsync_ShouldReturnTheTextAsGiven_WithUsageLatencyAndFinishReason()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero));
        var client = new FakeChatClient
        {
            Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "```json\n{\"label\":\"food\"}\n```"))
            {
                FinishReason = ChatFinishReason.Stop,
                Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 40, CachedInputTokenCount = 100, ReasoningTokenCount = 25 },
            },
            During = _ =>
            {
                time.Advance(TimeSpan.FromMilliseconds(250));
                return Task.CompletedTask;
            },
        };

        var result = await Callers.Fake(client, time: time).CallAsync(Request("groq/openai/gpt-oss-20b"));

        result.Should().BeEquivalentTo(new AiCallResult
        {
            Model = new AiModelRef("groq", "openai/gpt-oss-20b"),
            StartedAtUtc = new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            LatencyMs = 250,
            Text = "```json\n{\"label\":\"food\"}\n```",
            FinishReason = "stop",
            InputTokens = 120,
            OutputTokens = 40,
            CacheReadTokens = 100,
            CacheWriteTokens = 0,
            ReasoningTokens = 25,
        });
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CallAsync_ShouldReadAnthropicCacheWrites_AndTheRawStopReason()
    {
        var client = new FakeChatClient
        {
            Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "{}"))
            {
                FinishReason = ChatFinishReason.Length,
                RawRepresentation = Message.FromRawUnchecked(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    """{"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5","content":[],"stop_reason":"max_tokens","stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":5,"cache_creation_input_tokens":300,"cache_read_input_tokens":200}}""")!),
                Usage = new UsageDetails
                {
                    InputTokenCount = 510,
                    OutputTokenCount = 5,
                    CachedInputTokenCount = 200,
                    AdditionalCounts = new() { ["CacheCreationInputTokens"] = 300 },
                },
            },
        };

        var result = await Callers.Fake(client).CallAsync(Request());

        result.FinishReason.Should().Be("max_tokens");
        result.Should().Match<AiCallResult>(r =>
            r.InputTokens == 510 && r.CacheReadTokens == 200 && r.CacheWriteTokens == 300 && r.OutputTokens == 5);
    }

    [Fact]
    public async Task CallAsync_ShouldAddCachedTokensToInput_WhenTheClientReportsThemApart()
    {
        var client = new FakeChatClient
        {
            Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "x"))
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    CachedInputTokenCount = 200,
                    AdditionalCounts = new() { ["CacheCreationInputTokens"] = 300 },
                },
            },
        };

        var result = await Callers.Fake(client).CallAsync(Request());

        result.InputTokens.Should().Be(510);
    }

    [Theory]
    [InlineData("content_filter")]
    [InlineData("refusal")]
    public async Task CallAsync_ShouldAnswerContentFiltered_WithNoText_WhenTheModelRefuses(string reason)
    {
        var client = new FakeChatClient
        {
            Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "I can't help with that."))
            {
                FinishReason = new ChatFinishReason(reason),
                Usage = new UsageDetails { InputTokenCount = 12, OutputTokenCount = 8 },
            },
        };

        var result = await Callers.Fake(client).CallAsync(Request());

        result.Should().Match<AiCallResult>(r => r.ErrorKind == AiErrorKind.ContentFiltered && r.Text == null
            && r.FinishReason == reason && r.InputTokens == 12 && r.OutputTokens == 8);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    public async Task CallAsync_ShouldAnswerInvalidResponse_WhenTheTextIsEmpty(string text)
    {
        var client = new FakeChatClient { Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) };

        var result = await Callers.Fake(client).CallAsync(Request());

        result.ErrorKind.Should().Be(AiErrorKind.InvalidResponse);
        result.Text.Should().BeNull();
    }

    [Fact]
    public async Task CallAsync_ShouldClassifyAThrow_AndKeepTheKeyOutOfTheError()
    {
        var client = new FakeChatClient
        {
            Throws = new HttpRequestException($"401 for key {Callers.AnthropicKey}: {new string('x', 900)}", null, System.Net.HttpStatusCode.Unauthorized),
        };

        var result = await Callers.Fake(client).CallAsync(Request());

        result.ErrorKind.Should().Be(AiErrorKind.Unauthorized);
        result.Error.Should().NotContain(Callers.AnthropicKey).And.Contain("***");
        result.Error!.Length.Should().Be(500);
    }

    [Fact]
    public async Task CallAsync_ShouldAnswerTimeout_WhenTheProviderTimeoutPasses()
    {
        var providers = Callers.Providers();
        providers.Providers["anthropic"].TimeoutSeconds = 1;
        var client = new FakeChatClient { During = token => Task.Delay(Timeout.Infinite, token) };

        var result = await Callers.Fake(client, providers, TimeProvider.System).CallAsync(Request());

        result.ErrorKind.Should().Be(AiErrorKind.Timeout);
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(900);
    }

    [Fact]
    public async Task CallAsync_ShouldRethrow_WhenTheCallerCancels()
    {
        using var cancel = new CancellationTokenSource();
        var client = new FakeChatClient
        {
            During = async token =>
            {
                await cancel.CancelAsync();
                await Task.Delay(Timeout.Infinite, token);
            },
        };

        var call = () => Callers.Fake(client).CallAsync(Request(), cancel.Token);

        await call.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(AiErrorKind.Network, true)]
    [InlineData(AiErrorKind.Timeout, true)]
    [InlineData(AiErrorKind.RateLimited, true)]
    [InlineData(AiErrorKind.Unavailable, true)]
    [InlineData(AiErrorKind.Unauthorized, false)]
    [InlineData(AiErrorKind.BadRequest, false)]
    [InlineData(AiErrorKind.ContentFiltered, false)]
    [InlineData(AiErrorKind.InvalidResponse, false)]
    [InlineData(AiErrorKind.SchemaMismatch, false)]
    [InlineData(AiErrorKind.NotConfigured, false)]
    [InlineData(AiErrorKind.Unknown, false)]
    public void IsTransient_ShouldHoldForPassingFailuresOnly(AiErrorKind kind, bool transient) =>
        AiRetry.IsTransient(kind).Should().Be(transient);
}
