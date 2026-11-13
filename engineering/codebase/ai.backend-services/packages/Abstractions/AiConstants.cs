namespace WoW.Two.Sdk.Beta.Be.Ai;

/// <summary>Holds the limits and well-known values every form of the AI contract shares.</summary>
public static class AiConstants
{
    /// <summary>The longest use case key: lowercase letters, digits and hyphens.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>The longest model reference, <c>{provider}/{model}</c>.</summary>
    public const int MaxModelRefLength = 128;

    /// <summary>The longest subject reference, such as <c>harvest:post:{id}@{revision}</c>.</summary>
    public const int MaxSubjectLength = 256;

    /// <summary>The label a versioned use case resolves when a caller names neither a version nor a label.</summary>
    public const string ProductionLabel = "production";

    /// <summary>The label for the version under evaluation.</summary>
    public const string CandidateLabel = "candidate";

    /// <summary>Micro-USD in one US dollar; every cost in the contract is an integer count of micro-USD.</summary>
    public const long MicroUsdPerUsd = 1_000_000;
}
