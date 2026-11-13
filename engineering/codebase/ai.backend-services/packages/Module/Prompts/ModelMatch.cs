using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;

/// <summary>Decides which prompt row serves a model: a row's model match is none (every model), an exact model id, or a
/// prefix ending in <c>*</c>; the most specific fitting row wins, then the highest version.</summary>
internal static class ModelMatch
{
    /// <summary>Holds the specificity of a row with no match, which fits every model.</summary>
    public const int Generic = 0;

    /// <summary>Holds the specificity of an exact match, above every prefix.</summary>
    public const int Exact = int.MaxValue;

    /// <summary>Holds the specificity of a match that does not fit.</summary>
    public const int NoFit = -1;

    /// <summary>Normalizes a match: trimmed, none when blank.</summary>
    /// <param name="match">The match as given.</param>
    public static string? Normalize(string? match) => string.IsNullOrWhiteSpace(match) ? null : match.Trim();

    /// <summary>Gets whether a normalized match is well formed: at most <see cref="AiConstants.MaxModelRefLength"/>
    /// characters, no whitespace, and <c>*</c> only as the last character after at least one other.</summary>
    /// <param name="match">The normalized match; none is valid.</param>
    public static bool IsValid(string? match)
    {
        if (match is null)
            return true;
        if (match.Length > AiConstants.MaxModelRefLength || match.Any(char.IsWhiteSpace))
            return false;

        var star = match.IndexOf('*', StringComparison.Ordinal);
        return star < 0 || (star == match.Length - 1 && star > 0);
    }

    /// <summary>Rates how specifically <paramref name="match"/> names <paramref name="model"/>: <see cref="Exact"/>, one
    /// more than the prefix length for a prefix, <see cref="Generic"/> for none, or <see cref="NoFit"/>.</summary>
    /// <param name="match">The row's model match.</param>
    /// <param name="model">The routed model id.</param>
    public static int Specificity(string? match, string model)
    {
        if (match is null)
            return Generic;
        if (match.EndsWith('*'))
        {
            var prefix = match[..^1];
            return model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 1 + prefix.Length : NoFit;
        }

        return string.Equals(match, model, StringComparison.OrdinalIgnoreCase) ? Exact : NoFit;
    }

    /// <summary>Picks the row that serves <paramref name="model"/> at or below <paramref name="targetVersion"/>: the most
    /// specific fitting match, then the highest version; none when no row fits.</summary>
    /// <param name="rows">The use case's rows.</param>
    /// <param name="model">The routed model id.</param>
    /// <param name="targetVersion">The highest version to consider.</param>
    public static PromptModel? Pick(IEnumerable<PromptModel> rows, string model, int targetVersion) =>
        rows.Where(row => row.Version <= targetVersion)
            .Select(row => (Row: row, Specificity: Specificity(row.ModelMatch, model)))
            .Where(candidate => candidate.Specificity != NoFit)
            .OrderByDescending(candidate => candidate.Specificity)
            .ThenByDescending(candidate => candidate.Row.Version)
            .Select(candidate => candidate.Row)
            .FirstOrDefault();
}
