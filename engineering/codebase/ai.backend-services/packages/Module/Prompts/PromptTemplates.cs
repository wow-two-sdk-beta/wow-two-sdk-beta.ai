using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;

/// <summary>Renders a prompt template's <c>{{name}}</c> placeholders from a call's variables.</summary>
/// <remarks>A name is letters, digits, <c>_</c>, <c>.</c> and <c>-</c>, matched exactly; spaces inside the braces are
/// ignored (<c>{{ title }}</c>). A variable present with a null value renders empty; a missing one is reported, never
/// rendered as text.</remarks>
internal static partial class PromptTemplates
{
    /// <summary>Lists the placeholder names a template uses, in order of first use.</summary>
    /// <param name="template">The template.</param>
    public static IReadOnlyList<string> Placeholders(string template) =>
        [.. PlaceholderPattern().Matches(template).Select(match => match.Groups["name"].Value).Distinct(StringComparer.Ordinal)];

    /// <summary>Renders a template, collecting the names no variable answers.</summary>
    /// <param name="template">The template.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="missing">Receives each name with no variable.</param>
    /// <returns>The rendered text; placeholders with no variable stay as they are.</returns>
    public static string Render(string template, IReadOnlyDictionary<string, string?> variables, ISet<string> missing)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(missing);

        return PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out var value))
                return value ?? string.Empty;

            missing.Add(name);
            return match.Value;
        });
    }

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z0-9_.\-]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
