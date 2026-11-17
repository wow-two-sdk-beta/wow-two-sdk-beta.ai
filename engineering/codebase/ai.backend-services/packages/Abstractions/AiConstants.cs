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

    /// <summary>The seed an eval run's sample is ranked by when the request names none, so reruns compare the same items.</summary>
    public const int DefaultSampleSeed = 0;

    /// <summary>The longest name of whoever requested an eval run.</summary>
    public const int MaxRequestedByLength = 256;

    /// <summary>The longest data scope, such as <c>tenant:{id}</c>.</summary>
    public const int MaxScopeLength = 128;

    /// <summary>The data scope of calls, sets and runs made without one: the single bucket of a single-tenant host. A filter
    /// names it to keep only those rows; a model reads it back as no scope.</summary>
    public const string Unscoped = "";

    /// <summary>The most images a prompt takes, and the highest per-call limit a module may set.</summary>
    public const int MaxImages = 20;

    /// <summary>The longest image URI.</summary>
    public const int MaxImageUriLength = 2048;

    /// <summary>The longest image key, such as <c>fss:{id}</c>.</summary>
    public const int MaxImageKeyLength = 256;

    /// <summary>The image media types every provider takes: JPEG, PNG, WebP and GIF.</summary>
    public static IReadOnlyList<string> ImageMediaTypes { get; } = ["image/jpeg", "image/png", "image/webp", "image/gif"];
}
