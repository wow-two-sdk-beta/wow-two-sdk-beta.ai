using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Ai.Tests.E2E;

[Collection(AiApiCollection.Name)]
public sealed class SystemStatusTests(AiApiFixture fixture)
{
    [Fact]
    public async Task Health_ShouldReturn200_WhenTheDatabaseAnswers()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Status_ShouldReturn200_WithServiceAndVersion()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.GetAsync("/api/system/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var status = body.RootElement.GetProperty("data");
        status.GetProperty("service").GetString().Should().Be("ai");
        status.GetProperty("status").GetString().Should().Be("ok");
        status.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace().And.NotBe("unknown");
    }
}
