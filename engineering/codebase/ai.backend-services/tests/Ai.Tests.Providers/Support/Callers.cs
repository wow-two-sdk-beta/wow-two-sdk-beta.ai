using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

namespace Ai.Tests.Providers.Support;

/// <summary>Builds callers over a fake inner client or a stub HTTP handler.</summary>
internal static class Callers
{
    public const string AnthropicKey = "test-anthropic-key-0001";
    public const string OpenAiKey = "test-openai-key-0002";

    public static AiProvidersOptions Providers()
    {
        var options = new AiProvidersOptions();
        options.Providers["anthropic"] = new AiProviderOptions { ApiKey = AnthropicKey };
        options.Providers["groq"] = new AiProviderOptions { Kind = "openai", BaseUrl = "https://api.groq.com/openai/v1", ApiKey = OpenAiKey };
        options.Providers["ollama"] = new AiProviderOptions();
        return options;
    }

    public static AiModelCaller Fake(FakeChatClient client, AiProvidersOptions? providers = null, TimeProvider? time = null)
    {
        var options = Options.Create(providers ?? Providers());
        return new AiModelCaller(options, new AiChatClients(options, new FakeInnerChatClientFactory(client)), time ?? new FakeTimeProvider());
    }

    public static AiModelCaller Wire(StubHandler handler, AiProvidersOptions? providers = null)
    {
        var options = Options.Create(providers ?? Providers());
        return new AiModelCaller(options, new AiChatClients(options, new AiInnerChatClientFactory(() => handler)), TimeProvider.System);
    }
}
