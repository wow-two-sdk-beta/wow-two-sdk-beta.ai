using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Providers;

/// <summary>Represents one named provider as the catalog shows it: never its key.</summary>
public sealed record AiProviderInfo
{
    /// <summary>Gets the provider name, the first part of a model reference.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the resolved kind (<see cref="AiProviderKinds"/>); none when the settings name no known kind.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets whether calls can reach the provider: a known kind, with a key unless it is Ollama.</summary>
    public bool Configured { get; init; }

    /// <summary>Gets the host of the base URL calls go to, the kind's public endpoint when none is set; never the path or
    /// query, which may carry credentials.</summary>
    public string? BaseUrlHost { get; init; }

    /// <summary>Gets why the provider cannot be called, when it cannot; never quotes the key.</summary>
    public string? Problem { get; init; }
}

/// <summary>Represents the models a provider answered with, or why it could not answer.</summary>
public sealed record AiProviderModels
{
    /// <summary>Gets the provider name.</summary>
    public required string Provider { get; init; }

    /// <summary>Gets the model ids the provider serves, sorted; empty on an error.</summary>
    public IReadOnlyList<string> Models { get; init; } = [];

    /// <summary>Gets why the listing failed; none when it answered.</summary>
    public AiErrorKind? ErrorKind { get; init; }

    /// <summary>Gets the provider's message, trimmed and key-free, when the listing failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets whether the provider answered.</summary>
    public bool Succeeded => ErrorKind is null;
}
