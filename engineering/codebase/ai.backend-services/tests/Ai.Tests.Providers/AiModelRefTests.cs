using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;

namespace Ai.Tests.Providers;

public sealed class AiModelRefTests
{
    [Theory]
    [InlineData("anthropic/claude-haiku-5-5", "anthropic", "claude-haiku-5-5")]
    [InlineData("groq/openai/gpt-oss-20b", "groq", "openai/gpt-oss-20b")]
    [InlineData(" Ollama / llama3.2 ", "ollama", "llama3.2")]
    public void TryParse_ShouldSplitAtTheFirstSlash(string value, string provider, string model)
    {
        AiModelRef.TryParse(value, out var reference).Should().BeTrue();

        reference.Should().Be(new AiModelRef(provider, model));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anthropic")]
    [InlineData("/claude")]
    [InlineData("anthropic/")]
    [InlineData("anthropic/ ")]
    public void TryParse_ShouldFail_WhenAPartIsMissing(string? value)
    {
        AiModelRef.TryParse(value, out var reference).Should().BeFalse();
        reference.Should().BeNull();
    }

    [Fact]
    public void ToString_ShouldJoinProviderAndModel() =>
        new AiModelRef("groq", "openai/gpt-oss-20b").ToString().Should().Be("groq/openai/gpt-oss-20b");
}
