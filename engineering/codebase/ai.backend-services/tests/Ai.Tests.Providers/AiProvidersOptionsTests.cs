using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ai.Tests.Providers.Support;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Providers;

/// <summary>Binding <c>Ai:Providers</c>, kind inference and the unconfigured state.</summary>
public sealed class AiProvidersOptionsTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private static ServiceProvider Services(IConfiguration configuration, Action<AiProvidersOptions>? configure = null,
        FakeChatClient? client = null, Action<IServiceCollection>? more = null)
    {
        var services = new ServiceCollection();
        if (client is not null)
            services.AddSingleton<IAiInnerChatClientFactory>(new FakeInnerChatClientFactory(client));
        services.AddAiProviders(configuration, configure);
        more?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddAiProviders_ShouldBindEveryChild_ByName()
    {
        using var provider = Services(Configuration(
            ("Ai:Providers:anthropic:ApiKey", "k1"),
            ("Ai:Providers:anthropic:TimeoutSeconds", "30"),
            ("Ai:Providers:Groq:Kind", "openai"),
            ("Ai:Providers:Groq:BaseUrl", "https://api.groq.com/openai/v1"),
            ("Ai:Providers:Groq:ApiKey", "k2"),
            ("Ai:Providers:ollama:BaseUrl", "http://gpu-box:11434")));

        var providers = provider.GetRequiredService<IOptions<AiProvidersOptions>>().Value.Providers;

        providers.Keys.Should().BeEquivalentTo("anthropic", "Groq", "ollama");
        providers["anthropic"].TimeoutSeconds.Should().Be(30);
        providers["groq"].Should().Match<AiProviderOptions>(groq =>
            groq.Kind == "openai" && groq.BaseUrl == "https://api.groq.com/openai/v1" && groq.TimeoutSeconds == 60);
        providers["ollama"].BaseUrl.Should().Be("http://gpu-box:11434");
    }

    [Fact]
    public void AddAiProviders_ShouldApplyTheCodeConfiguration_AfterTheBinding()
    {
        using var provider = Services(Configuration(("Ai:Providers:anthropic:TimeoutSeconds", "30")), options =>
        {
            options.Providers["anthropic"].ApiKey = "from-code";
            options.Providers["deepseek"] = new AiProviderOptions { Kind = "openai", ApiKey = "k" };
        });

        var providers = provider.GetRequiredService<IOptions<AiProvidersOptions>>().Value.Providers;

        providers["anthropic"].Should().Match<AiProviderOptions>(anthropic => anthropic.ApiKey == "from-code" && anthropic.TimeoutSeconds == 30);
        providers.Should().ContainKey("deepseek");
    }

    [Theory]
    [InlineData("anthropic", "", "anthropic")]
    [InlineData("OpenAI", "", "openai")]
    [InlineData("ollama", "", "ollama")]
    [InlineData("groq", "OpenAI", "openai")]
    [InlineData("anthropic", "ollama", "ollama")]
    [InlineData("groq", "", null)]
    [InlineData("groq", "gemini", null)]
    public void Resolve_ShouldTakeTheKind_ElseTheNameWhenItIsAKind(string name, string kind, string? expected) =>
        AiProviderKinds.Resolve(name, new AiProviderOptions { Kind = kind }).Should().Be(expected);

    [Fact]
    public void IsConfigured_ShouldNeedAKey_ExceptForOllama()
    {
        using var provider = Services(Configuration(
            ("Ai:Providers:anthropic:TimeoutSeconds", "30"),
            ("Ai:Providers:openai:ApiKey", "k"),
            ("Ai:Providers:ollama:TimeoutSeconds", "120"),
            ("Ai:Providers:groq:ApiKey", "k")));
        var caller = provider.GetRequiredService<IAiModelCaller>();

        caller.IsConfigured("anthropic").Should().BeFalse("it has no key");
        caller.IsConfigured("openai").Should().BeTrue();
        caller.IsConfigured("OLLAMA").Should().BeTrue("Ollama needs no key, and names compare case-insensitively");
        caller.IsConfigured("groq").Should().BeFalse("its name is no kind and it sets none");
        caller.IsConfigured("missing").Should().BeFalse();
        provider.GetRequiredService<IAiChatClients>().IsConfigured("openai").Should().BeTrue();
    }

    [Theory]
    [InlineData("anthropic", "no API key")]
    [InlineData("groq", "no known kind")]
    [InlineData("missing", "not configured")]
    public async Task CallAsync_ShouldAnswerNotConfigured_WithoutACall(string providerName, string error)
    {
        var client = new FakeChatClient();
        using var provider = Services(Configuration(("Ai:Providers:anthropic:TimeoutSeconds", "30"), ("Ai:Providers:groq:ApiKey", "k")),
            client: client);

        var result = await provider.GetRequiredService<IAiModelCaller>().CallAsync(new AiCallRequest
        {
            Model = new AiModelRef(providerName, "m"),
            System = "s",
            User = "u",
        });

        result.ErrorKind.Should().Be(AiErrorKind.NotConfigured);
        result.Error.Should().Contain(error);
        result.Succeeded.Should().BeFalse();
        client.Calls.Should().Be(0);
    }

    [Fact]
    public void Get_ShouldThrow_WhenTheProviderIsUnconfigured()
    {
        using var provider = Services(Configuration(("Ai:Providers:anthropic:TimeoutSeconds", "30")), client: new FakeChatClient());

        var get = () => provider.GetRequiredService<IAiChatClients>().Get(new AiModelRef("anthropic", "m"));

        get.Should().Throw<InvalidOperationException>().WithMessage("*no API key*");
    }

    [Fact]
    public void Get_ShouldCacheOneClientPerProviderAndModel()
    {
        var client = new FakeChatClient();
        var factory = new FakeInnerChatClientFactory(client);
        using var provider = Services(Configuration(("Ai:Providers:ollama:TimeoutSeconds", "30")),
            more: services => services.AddSingleton<IAiInnerChatClientFactory>(factory));
        var clients = provider.GetRequiredService<IAiChatClients>();

        var first = clients.Get(new AiModelRef("ollama", "llama3.2"));
        var again = clients.Get(new AiModelRef("OLLAMA", "llama3.2"));
        var other = clients.Get(new AiModelRef("ollama", "qwen3"));

        again.Should().BeSameAs(first);
        other.Should().NotBeSameAs(first);
        factory.Created.Should().Equal(("ollama", "ollama", "llama3.2"), ("ollama", "ollama", "qwen3"));
        first.GetService<FakeChatClient>().Should().BeSameAs(client, "the factory's client sits inside the telemetry wrapper");
    }

    [Fact]
    public async Task AddAiChatClient_ShouldRegisterTheFactoryClient_WhichTheContainerDoesNotDispose()
    {
        var client = new FakeChatClient();
        var provider = Services(Configuration(("Ai:Providers:ollama:TimeoutSeconds", "30")), client: client,
            more: services => services.AddAiChatClient("Ollama", "llama3.2"));

        var chat = provider.GetRequiredService<IChatClient>();
        var response = await chat.GetResponseAsync("hi");

        response.Text.Should().Be("ok");
        client.Calls.Should().Be(1);
        chat.GetService<FakeChatClient>().Should().BeSameAs(client);

        await provider.DisposeAsync();
        client.Disposed.Should().BeTrue("the factory disposes what it built, once, when the container ends");
    }
}
