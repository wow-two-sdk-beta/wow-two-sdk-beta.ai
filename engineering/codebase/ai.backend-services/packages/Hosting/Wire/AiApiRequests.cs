namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Wire;

/// <summary>Represents the body of <see cref="AiRouteConstants.Label"/>: the version a label moves to.</summary>
public sealed record AiLabelMoveRequest
{
    /// <summary>Gets the version; it must exist.</summary>
    public required int Version { get; init; }

    /// <summary>Gets who moved it.</summary>
    public string? By { get; init; }
}

/// <summary>Represents the body of <see cref="AiRouteConstants.InferenceForget"/>: the subject whose stored answers to clear
/// in the caller's data scopes.</summary>
public sealed record AiForgetRequest
{
    /// <summary>Gets the subject reference.</summary>
    public required string Subject { get; init; }
}

/// <summary>Represents the body of <see cref="AiRouteConstants.EvalItemExpected"/>: an item's new expected answer.</summary>
public sealed record EvalRelabelRequest
{
    /// <summary>Gets the expected answer as a JSON object, in a string.</summary>
    public required string Expected { get; init; }

    /// <summary>Gets who changed it.</summary>
    public string? By { get; init; }
}

/// <summary>Represents a success body: the payload under <c>data</c>, as the be sdk's API envelope writes it.</summary>
/// <typeparam name="T">The payload.</typeparam>
/// <param name="Data">The payload.</param>
public sealed record AiEnvelope<T>(T Data);
