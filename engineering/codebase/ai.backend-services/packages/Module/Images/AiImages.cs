using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Images;

/// <summary>Checks image references against the module's limits, and reads an image's media type from its bytes.</summary>
internal static class AiImages
{
    /// <summary>Checks a call's or an item's image references: at most <see cref="AiModuleOptions.MaxImagesPerCall"/>,
    /// each a URI or a key (not both), an <c>http(s)</c> URI absolute, a SHA-256 of 64 hex characters, a media type the
    /// module takes.</summary>
    /// <param name="images">The references.</param>
    /// <param name="options">The module's settings.</param>
    /// <returns>The validation error, or none.</returns>
    public static AppError? Check(IReadOnlyList<AiImageRef>? images, AiModuleOptions options)
    {
        if (images is null || images.Count == 0)
            return null;
        if (images.Count > options.MaxImagesPerCall)
            return AiErrors.Invalid(options.MaxImagesPerCall == 0
                ? "This module takes no images."
                : $"A call carries at most {options.MaxImagesPerCall} image{(options.MaxImagesPerCall == 1 ? "" : "s")}, not {images.Count}.");

        for (var index = 0; index < images.Count; index++)
        {
            if (CheckOne(images[index], index, options) is { } error)
                return error;
        }

        return null;
    }

    /// <summary>Gets the media types the module takes: its setting, else every one providers take.</summary>
    /// <param name="options">The module's settings.</param>
    public static IReadOnlyList<string> Allowed(AiModuleOptions options) =>
        options.ImageMediaTypes is { Length: > 0 } configured ? configured : AiConstants.ImageMediaTypes;

    /// <summary>Normalises a media type: lower case, parameters dropped, <c>image/jpg</c> read as <c>image/jpeg</c>.</summary>
    /// <param name="mediaType">The media type.</param>
    /// <returns>The normalised type, or none when blank.</returns>
    public static string? Normalize(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return null;

        var bare = mediaType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return bare switch
        {
            "" => null,
            "image/jpg" or "image/pjpeg" => "image/jpeg",
            _ => bare,
        };
    }

    /// <summary>Reads an image's media type from its first bytes: JPEG, PNG, GIF or WebP.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The media type, or none when the bytes are none of those.</returns>
    public static string? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature))
            return "image/png";
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return "image/gif";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    /// <summary>Gets whether a text is an absolute <c>http</c> or <c>https</c> URI.</summary>
    /// <param name="text">The text.</param>
    /// <param name="uri">The URI.</param>
    public static bool TryHttp(string? text, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text) || !System.Uri.TryCreate(text, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != System.Uri.UriSchemeHttp && parsed.Scheme != System.Uri.UriSchemeHttps))
            return false;
        uri = parsed;
        return true;
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static AppError? CheckOne(AiImageRef? image, int index, AiModuleOptions options)
    {
        var position = $"Image {index + 1}";
        if (image is null)
            return AiErrors.Invalid($"{position} is empty.");

        var hasUri = !string.IsNullOrWhiteSpace(image.Uri);
        var hasKey = !string.IsNullOrWhiteSpace(image.Key);
        if (hasUri == hasKey)
            return AiErrors.Invalid($"{position} names {(hasUri ? "both a URI and a key" : "neither a URI nor a key")}; give exactly one.");
        if (hasUri && image.Uri!.Length > AiConstants.MaxImageUriLength)
            return AiErrors.Invalid($"{position}'s URI is over {AiConstants.MaxImageUriLength} characters.");
        if (hasUri && !TryHttp(image.Uri, out _))
            return AiErrors.Invalid($"{position}'s URI '{image.Uri}' is not an absolute http or https URI; a private image goes by key.");
        if (hasKey && image.Key!.Length > AiConstants.MaxImageKeyLength)
            return AiErrors.Invalid($"{position}'s key is over {AiConstants.MaxImageKeyLength} characters.");
        if (image.Sha256 is { } sha && (sha.Length != 64 || !sha.All(char.IsAsciiHexDigit)))
            return AiErrors.Invalid($"{position}'s SHA-256 must be 64 hex characters.");
        if (Normalize(image.MediaType) is { } mediaType && !Allowed(options).Contains(mediaType, StringComparer.Ordinal))
            return AiErrors.Invalid($"{position}'s media type '{image.MediaType}' is not one of {string.Join(", ", Allowed(options))}.");

        return null;
    }

    /// <summary>Returns whether the module's own fetch may read <paramref name="uri"/>: its host is listed, exactly or
    /// under a <c>*.</c> entry; an empty list allows nothing.</summary>
    /// <param name="uri">The image's absolute HTTP(S) URI.</param>
    /// <param name="hosts">The allowed hosts.</param>
    internal static bool FetchAllowed(Uri uri, IReadOnlyCollection<string>? hosts)
    {
        if (hosts is null || hosts.Count == 0)
            return false;
        var host = uri.IdnHost;
        foreach (var entry in hosts)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            var allowed = entry.Trim();
            if (allowed.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = allowed[1..];
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && host.Length > suffix.Length)
                    return true;
            }
            else if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
