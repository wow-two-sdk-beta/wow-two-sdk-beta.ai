namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Represents the call parameters a prompt row carries with its text.</summary>
public sealed record PromptParameters
{
    /// <summary>Gets the sampling temperature; none leaves the provider's default.</summary>
    public double? Temperature { get; init; }

    /// <summary>Gets the most output tokens; none takes the module's default.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Gets the reasoning effort, <c>none</c>, <c>low</c>, <c>medium</c> or <c>high</c>; none leaves the provider's default.</summary>
    public string? Effort { get; init; }

    /// <summary>Gets the most images the prompt sends with its user message, 0 to <see cref="AiConstants.MaxImages"/>; none
    /// or 0 takes text only, so a call's images are ignored and stay out of its input hash.</summary>
    public int? MaxImages { get; init; }
}
