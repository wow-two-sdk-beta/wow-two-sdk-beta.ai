using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;

namespace Ai.Tests.Unit.Prompts;

public sealed class PromptTemplatesTests
{
    [Fact]
    public void Render_ShouldReplaceEveryPlaceholder_WithItsVariable()
    {
        var missing = new SortedSet<string>();

        var rendered = PromptTemplates.Render("Title: {{title}}\nText: {{ text }} ({{title}})", Vars(("title", "Runway"), ("text", "Tashkent")), missing);

        rendered.Should().Be("Title: Runway\nText: Tashkent (Runway)");
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Render_ShouldReportMissingVariables_AndLeaveTheirPlaceholders()
    {
        var missing = new SortedSet<string>();

        var rendered = PromptTemplates.Render("{{language}}: {{title}}", Vars(("title", "Runway")), missing);

        rendered.Should().Be("{{language}}: Runway");
        missing.Should().Equal("language");
    }

    [Fact]
    public void Render_ShouldRenderANullVariableEmpty_AndMatchNamesExactly()
    {
        var missing = new SortedSet<string>();

        var rendered = PromptTemplates.Render("[{{note}}] {{Title}}", Vars(("note", null), ("title", "x")), missing);

        rendered.Should().Be("[] {{Title}}");
        missing.Should().Equal("Title");
    }

    [Fact]
    public void Render_ShouldLeaveSingleBracesAndJsonExamplesAlone()
    {
        var missing = new SortedSet<string>();

        var rendered = PromptTemplates.Render("""Answer {"relevance": "close"} for {{title}}""", Vars(("title", "a")), missing);

        rendered.Should().Be("""Answer {"relevance": "close"} for a""");
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Placeholders_ShouldListEachNameOnce_InOrder()
    {
        PromptTemplates.Placeholders("{{b}} {{a}} {{ b }} {{post.text}}").Should().Equal("b", "a", "post.text");
    }

    private static Dictionary<string, string?> Vars(params (string Name, string? Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Name, pair => pair.Value);
}
