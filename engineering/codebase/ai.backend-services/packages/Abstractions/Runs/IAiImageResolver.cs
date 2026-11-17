namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Turns an image reference into its bytes: the host's seam for images a model cannot fetch, such as SCFD's
/// private file storage (<c>fss:{id}</c>).</summary>
/// <remarks>
///   - a host registers one or more; the module asks each in turn and takes the first answer, then falls back to its own
///     HTTP(S) fetch for a public <see cref="AiImageRef.Uri"/>
///   - the module checks every answer against its limits (size, media type) and the reference's
///     <see cref="AiImageRef.Sha256"/>, so a resolver only reads
///   - resolved per call and per eval item; the bytes never reach the database
/// </remarks>
public interface IAiImageResolver
{
    /// <summary>Resolves <paramref name="image"/> to its bytes.</summary>
    /// <param name="image">The reference.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The image; none when this resolver does not handle the reference, or the image does not exist.</returns>
    Task<AiImageContent?> ResolveAsync(AiImageRef image, CancellationToken cancellationToken = default);
}
