namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Points at one image a call sends with its prompt: a public <see cref="Uri"/>, or a <see cref="Key"/> the host's
/// <see cref="IAiImageResolver"/> understands. A reference carries no bytes; the module resolves it when a prompt takes
/// images.</summary>
/// <remarks>
///   - give exactly one of <see cref="Uri"/> and <see cref="Key"/>
///   - an eval item and a set export keep the reference only, never the bytes
/// </remarks>
public sealed record AiImageRef
{
    /// <summary>Gets an absolute <c>http</c> or <c>https</c> URI the module fetches, such as
    /// <c>https://cdn.example.com/poster.jpg</c>; at most <see cref="AiConstants.MaxImageUriLength"/> characters.</summary>
    public string? Uri { get; init; }

    /// <summary>Gets a stable key the host resolves, such as <c>fss:{id}</c> for an image in SCFD's file storage; at most
    /// <see cref="AiConstants.MaxImageKeyLength"/> characters.</summary>
    public string? Key { get; init; }

    /// <summary>Gets the media type, such as <c>image/jpeg</c>, when known; the module reads it from the bytes when they tell.</summary>
    public string? MediaType { get; init; }

    /// <summary>Gets the SHA-256 of the image's bytes, 64 hex characters, when known; a resolved image that does not match
    /// it is refused.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Writes the reference as its URI or its key.</summary>
    public override string ToString() => Uri ?? Key ?? string.Empty;
}
