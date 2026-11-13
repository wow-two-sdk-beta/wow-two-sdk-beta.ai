namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Represents a model reference, <c>{provider}/{model}</c>, such as <c>anthropic/claude-haiku-5-5</c>.</summary>
/// <param name="Provider">The configured provider name.</param>
/// <param name="Model">The provider's model id.</param>
public sealed record AiModelRef(string Provider, string Model)
{
    /// <summary>Parses <c>{provider}/{model}</c>; the model may itself hold slashes (<c>groq/openai/gpt-oss-20b</c>).</summary>
    /// <param name="value">The reference.</param>
    /// <param name="reference">The parsed reference.</param>
    /// <returns>Whether both parts are present.</returns>
    public static bool TryParse(string? value, out AiModelRef? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var slash = value.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == value.Length - 1)
            return false;
        var parsed = new AiModelRef(value[..slash].Trim().ToLowerInvariant(), value[(slash + 1)..].Trim());
        if (parsed.Provider.Length == 0 || parsed.Model.Length == 0)
            return false;
        reference = parsed;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Provider}/{Model}";
}
