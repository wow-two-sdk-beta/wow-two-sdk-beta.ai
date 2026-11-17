using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Images;

/// <summary>Holds one resolved and checked image: its reference, its bytes with their media type, and their SHA-256.</summary>
/// <param name="Ref">The reference it was resolved from.</param>
/// <param name="Content">The bytes, with the media type the module settled on.</param>
/// <param name="Sha256">The SHA-256 of the bytes, lowercase hex.</param>
internal sealed record LoadedImage(AiImageRef Ref, AiImageContent Content, string Sha256);

/// <summary>Resolves image references to checked bytes: the host's <see cref="IAiImageResolver"/>s first, in the order they
/// were registered, then the module's own HTTP(S) fetch for a public URI.</summary>
/// <remarks>
///   - every image is checked whoever resolved it: at most <see cref="AiModuleOptions.MaxImageBytes"/>, bytes that are a
///     JPEG, PNG, WebP or GIF of a type the module takes (the bytes decide the media type, never a header), and the
///     reference's SHA-256 when it names one
///   - the fetch takes <see cref="AiModuleConstants.ImageHttpClientName"/>'s client, stops reading past the size limit and
///     gives up after <see cref="AiModuleOptions.ImageFetchTimeout"/>
///   - images resolve one after another, in the caller's scope, so a host resolver may use scoped services
/// </remarks>
/// <param name="resolvers">The host's resolvers.</param>
/// <param name="http">Creates the fetch's client.</param>
/// <param name="options">The module's limits.</param>
internal sealed class AiImageLoader(IEnumerable<IAiImageResolver> resolvers, IHttpClientFactory http, IOptions<AiModuleOptions> options)
{
    /// <summary>Resolves and checks <paramref name="images"/>, in order.</summary>
    /// <param name="images">The references, already shape-checked (<see cref="AiImages.Check"/>).</param>
    /// <param name="cancellationToken">The token that cancels the reads.</param>
    /// <returns>The images; <see cref="AiErrors.ImageNotResolved"/> for one no resolver reads, a validation error for one
    /// over the limits or off its SHA-256.</returns>
    public async Task<Result<IReadOnlyList<LoadedImage>>> LoadAsync(IReadOnlyList<AiImageRef> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        var loaded = new List<LoadedImage>(images.Count);
        foreach (var image in images)
        {
            var one = await LoadOneAsync(image, cancellationToken);
            if (one is Result<LoadedImage>.Failure { Error: var error })
                return Result<IReadOnlyList<LoadedImage>>.Fail(error);
            loaded.Add(((Result<LoadedImage>.Success)one).Value);
        }

        return Result<IReadOnlyList<LoadedImage>>.Ok(loaded);
    }

    private async Task<Result<LoadedImage>> LoadOneAsync(AiImageRef image, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var name = image.ToString();
        AiImageContent? content = null;
        foreach (var resolver in resolvers)
        {
            try
            {
                content = await resolver.ResolveAsync(image, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return Result<LoadedImage>.Fail(AiErrors.ImageNotResolved(name, exception.Message));
            }

            if (content is not null)
                break;
        }

        if (content is null && AiImages.TryHttp(image.Uri, out var uri))
        {
            if (!AiImages.FetchAllowed(uri!, settings.ImageFetchHosts))
                return Result<LoadedImage>.Fail(AiErrors.ImageNotResolved(name, $"the host '{uri!.Host}' is not in Ai:Module:ImageFetchHosts."));
            var fetched = await FetchAsync(name, uri!, settings, cancellationToken);
            if (fetched is Result<AiImageContent>.Failure { Error: var fetchError })
                return Result<LoadedImage>.Fail(fetchError);
            content = ((Result<AiImageContent>.Success)fetched).Value;
        }

        if (content is null)
            return Result<LoadedImage>.Fail(AiErrors.ImageNotResolved(name, image.Key is null ? "nothing resolves it." : "no image resolver handles that key."));
        if (content.Data.IsEmpty)
            return Result<LoadedImage>.Fail(AiErrors.Invalid($"The image '{name}' is empty."));
        if (content.Data.Length > settings.MaxImageBytes)
            return Result<LoadedImage>.Fail(TooLarge(name, settings));

        // The bytes decide the type: a provider refuses an image whose declared type differs from its bytes.
        if (AiImages.Sniff(content.Data.Span) is not { } mediaType)
            return Result<LoadedImage>.Fail(AiErrors.Invalid($"The image '{name}' is not a JPEG, PNG, WebP or GIF image."));
        var allowed = AiImages.Allowed(settings);
        if (!allowed.Contains(mediaType, StringComparer.Ordinal))
            return Result<LoadedImage>.Fail(AiErrors.Invalid($"The image '{name}' is '{mediaType}', not one of {string.Join(", ", allowed)}."));

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content.Data.Span));
        if (image.Sha256 is { } expected && !string.Equals(expected, sha256, StringComparison.OrdinalIgnoreCase))
            return Result<LoadedImage>.Fail(AiErrors.Invalid($"The image '{name}' no longer matches its SHA-256."));

        return Result<LoadedImage>.Ok(new LoadedImage(image, content with { MediaType = mediaType }, sha256));
    }

    /// <summary>Fetches a public image: a GET that must answer 2xx within the timeout and stay under the size limit.</summary>
    private async Task<Result<AiImageContent>> FetchAsync(string name, Uri uri, AiModuleOptions settings, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.ImageFetchTimeout);
        try
        {
            var client = http.CreateClient(AiModuleConstants.ImageHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return Result<AiImageContent>.Fail(AiErrors.ImageNotResolved(name, $"the host answered {(int)response.StatusCode}."));
            if (response.Content.Headers.ContentLength is { } length && length > settings.MaxImageBytes)
                return Result<AiImageContent>.Fail(TooLarge(name, settings));

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > settings.MaxImageBytes)
                    return Result<AiImageContent>.Fail(TooLarge(name, settings));
            }

            return Result<AiImageContent>.Ok(new AiImageContent(buffer.ToArray(), response.Content.Headers.ContentType?.MediaType));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<AiImageContent>.Fail(AiErrors.ImageNotResolved(name, $"the fetch took over {settings.ImageFetchTimeout.TotalSeconds:0.#} s."));
        }
        catch (HttpRequestException exception)
        {
            return Result<AiImageContent>.Fail(AiErrors.ImageNotResolved(name, exception.Message));
        }
    }

    private static Foundation.Errors.AppError TooLarge(string name, AiModuleOptions settings) =>
        AiErrors.Invalid($"The image '{name}' is over the limit of {settings.MaxImageBytes} bytes.");
}
