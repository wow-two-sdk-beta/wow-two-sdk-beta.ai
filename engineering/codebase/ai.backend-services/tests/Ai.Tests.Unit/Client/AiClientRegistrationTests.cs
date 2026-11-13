using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Client;

namespace Ai.Tests.Unit.Client;

public sealed class AiClientRegistrationTests
{
    [Fact]
    public void AddAiClient_ShouldRegisterTheNamedClient_OnTheServiceRoot()
    {
        var services = new ServiceCollection();
        services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.test/root"));
        using var provider = services.BuildServiceProvider();

        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(AiClientConstants.HttpClientName);

        http.BaseAddress.Should().Be(new Uri("https://ai.example.test/root/"));
    }

    [Fact]
    public void AddAiClient_ShouldRefuse_ARelativeRoot()
    {
        var services = new ServiceCollection();
        services.AddAiClient(options => options.BaseAddress = new Uri("/relative", UriKind.Relative));
        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<IOptions<AiClientOptions>>().Value;

        read.Should().Throw<OptionsValidationException>();
    }
}
