using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Ai.Tests.Providers.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Providers;

/// <summary>The provider catalog: every configured provider described without its key, and each kind's live model list
/// read through its real SDK against a stub HTTP handler.</summary>
public sealed class AiProviderCatalogTests
{
    private static AiProvidersOptions Providers()
    {
        var options = Callers.Providers();
        options.Providers["openai"] = new AiProviderOptions();
        options.Providers["odd"] = new AiProviderOptions { Kind = "nope", ApiKey = "odd-key-0003" };
        options.Providers["anthropic"].BaseUrl = null;
        return options;
    }

    private static AiProviderCatalog Catalog(StubHandler handler, AiProvidersOptions? providers = null) =>
        new(Options.Create(providers ?? Providers()), new AiInnerChatClientFactory(() => handler));

    [Fact]
    public void List_ShouldDescribeEveryProvider_WithItsKindHostAndState_ButNeverItsKey()
    {
        var catalog = Catalog(new StubHandler(HttpStatusCode.OK, "{}"));

        var providers = catalog.List();

        providers.Select(provider => provider.Name).Should().Equal("anthropic", "groq", "odd", "ollama", "openai");
        var byName = providers.ToDictionary(provider => provider.Name);
        byName["anthropic"].Should().BeEquivalentTo(new AiProviderInfo { Name = "anthropic", Kind = "anthropic", Configured = true, BaseUrlHost = "api.anthropic.com" });
        byName["groq"].Should().BeEquivalentTo(new AiProviderInfo { Name = "groq", Kind = "openai", Configured = true, BaseUrlHost = "api.groq.com" });
        byName["ollama"].Should().BeEquivalentTo(new AiProviderInfo { Name = "ollama", Kind = "ollama", Configured = true, BaseUrlHost = "localhost" });
        byName["openai"].Configured.Should().BeFalse();
        byName["openai"].BaseUrlHost.Should().Be("api.openai.com");
        byName["openai"].Problem.Should().Contain("no API key");
        byName["odd"].Kind.Should().BeNull();
        byName["odd"].Configured.Should().BeFalse();

        var text = JsonSerializer.Serialize(providers);
        // Compared as flags, so no credential reaches the output.
        text.Contains(Callers.AnthropicKey, StringComparison.Ordinal).Should().BeFalse();
        text.Contains(Callers.OpenAiKey, StringComparison.Ordinal).Should().BeFalse();
        text.Contains("odd-key-0003", StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public async Task ListModels_ShouldReadAnthropicsModelsEndpoint_ThroughItsSdk()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"data":[{"id":"claude-sonnet-5-5","type":"model","display_name":"Claude Sonnet 5.5","created_at":"2026-10-01T00:00:00Z"},
                     {"id":"claude-haiku-5-5","type":"model","display_name":"Claude Haiku 5.5","created_at":"2026-10-01T00:00:00Z"}],
             "has_more":false,"first_id":"claude-sonnet-5-5","last_id":"claude-haiku-5-5"}
            """);

        var models = await Catalog(handler).ListModelsAsync("Anthropic");

        models.Succeeded.Should().BeTrue(models.Error);
        models.Provider.Should().Be("anthropic");
        models.Models.Should().Equal("claude-haiku-5-5", "claude-sonnet-5-5");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.GetLeftPart(UriPartial.Path).Should().Be("https://api.anthropic.com/v1/models");
        (handler.LastRequest.Headers.Authorization?.Parameter == Callers.AnthropicKey).Should().BeTrue();
    }

    [Fact]
    public async Task ListModels_ShouldReadAnOpenAiCompatibleHostsModels_AtItsBaseUrl()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"object":"list","data":[{"id":"openai/gpt-oss-20b","object":"model","created":1760000000,"owned_by":"groq"},
                                      {"id":"llama-4-scout","object":"model","created":1760000000,"owned_by":"meta"}]}
            """);

        var models = await Catalog(handler).ListModelsAsync("groq");

        models.Models.Should().Equal("llama-4-scout", "openai/gpt-oss-20b");
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.groq.com/openai/v1/models");
    }

    [Fact]
    public async Task ListModels_ShouldReadOllamasLocalTags_WithNoKey()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"models":[{"name":"llama3.2:latest","model":"llama3.2:latest","modified_at":"2026-10-01T00:00:00Z","size":2019393189,
                        "digest":"a80c4f17acd5","details":{"format":"gguf","family":"llama"}}]}
            """);

        var models = await Catalog(handler).ListModelsAsync("ollama");

        models.Models.Should().Equal("llama3.2:latest");
        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://localhost:11434/api/tags");
    }

    [Fact]
    public async Task ListModels_ShouldClassifyAFailure_AndKeepTheKeyOut()
    {
        var unauthorized = new StubHandler(HttpStatusCode.Unauthorized,
            $$$"""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key {{{Callers.AnthropicKey}}}"}}""");
        var unreachable = new StubHandler(HttpStatusCode.OK, "{}") { Throws = new HttpRequestException("Connection refused") };

        var refused = await Catalog(unauthorized).ListModelsAsync("anthropic");
        var down = await Catalog(unreachable).ListModelsAsync("ollama");

        refused.ErrorKind.Should().Be(AiErrorKind.Unauthorized);
        refused.Models.Should().BeEmpty();
        refused.Error.Should().Contain("401");
        refused.Error!.Contains(Callers.AnthropicKey, StringComparison.Ordinal).Should().BeFalse();
        down.ErrorKind.Should().Be(AiErrorKind.Network);
    }

    [Fact]
    public async Task ListModels_ShouldAnswerNotConfigured_WithoutACall()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var catalog = Catalog(handler);

        var keyless = await catalog.ListModelsAsync("openai");
        var unknown = await catalog.ListModelsAsync("mistral");

        keyless.ErrorKind.Should().Be(AiErrorKind.NotConfigured);
        unknown.ErrorKind.Should().Be(AiErrorKind.NotConfigured);
        unknown.Error.Should().Contain("mistral");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public void AddAiProviders_ShouldRegisterTheCatalog_OverTheBoundProviders()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Providers:ollama:BaseUrl"] = "http://gpu-box:11434",
        }).Build();
        using var services = new ServiceCollection().AddAiProviders(configuration).BuildServiceProvider();

        var providers = services.GetRequiredService<IAiProviderCatalog>().List();

        providers.Should().ContainSingle().Which.Should().BeEquivalentTo(new AiProviderInfo
        {
            Name = "ollama",
            Kind = "ollama",
            Configured = true,
            BaseUrlHost = "gpu-box",
        });
    }
}
