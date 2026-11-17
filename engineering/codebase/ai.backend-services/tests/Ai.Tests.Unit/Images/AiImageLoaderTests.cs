using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Images;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Ai.Tests.Unit.Images;

/// <summary>The loader over host resolvers and a stub HTTP handler: who resolves what, the fetch's limits, and the checks
/// every image meets whoever resolved it.</summary>
public sealed class AiImageLoaderTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 9, 8, 7];

    [Fact]
    public async Task Load_ShouldTakeTheFirstResolverThatAnswers_AndHashTheBytes()
    {
        var first = new KeyResolver("fss:", Png, "image/png");
        var second = new KeyResolver("fss:", Jpeg, "image/jpeg");
        var handler = new Handler(_ => throw new InvalidOperationException("no fetch"));

        var images = Value(await Loader(handler, resolvers: [new KeyResolver("other:", Jpeg), first, second]).LoadAsync([new AiImageRef { Key = "fss:7" }], default));

        var image = images.Should().ContainSingle().Subject;
        image.Content.Data.ToArray().Should().Equal(Png);
        image.Content.MediaType.Should().Be("image/png");
        image.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Png)));
        second.Calls.Should().Be(0, "the first answer wins");
        handler.Calls.Should().Be(0, "a key is never fetched");
    }

    [Fact]
    public async Task Load_ShouldFetchAPublicUri_AndTrustTheBytesOverTheHeader()
    {
        var handler = new Handler(_ => Answer(Jpeg, "image/png"));

        var image = Value(await Loader(handler).LoadAsync([new AiImageRef { Uri = "https://cdn.example.test/poster.jpg" }], default)).Single();

        handler.Last!.Method.Should().Be(HttpMethod.Get);
        handler.Last.RequestUri!.ToString().Should().Be("https://cdn.example.test/poster.jpg");
        handler.Last.Headers.Accept.Should().ContainSingle().Which.MediaType.Should().Be("image/*");
        image.Content.MediaType.Should().Be("image/jpeg", "the bytes say JPEG whatever the header says");
        image.Content.Data.ToArray().Should().Equal(Jpeg);
    }

    [Fact]
    public async Task Load_ShouldFallBackToTheFetch_WhenNoResolverHandlesTheUri()
    {
        var handler = new Handler(_ => Answer(Png, "image/png"));

        var images = Value(await Loader(handler, resolvers: [new KeyResolver("fss:", Jpeg)]).LoadAsync([new AiImageRef { Uri = "https://a.test/p.png" }], default));

        images.Single().Content.MediaType.Should().Be("image/png");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Load_ShouldAnswerNotFound_ForAKeyNoResolverHandles_AndAFailedFetch()
    {
        var missing = Error(await Loader(new Handler(_ => Answer(Png))).LoadAsync([new AiImageRef { Key = "fss:404" }], default));
        var notFound = Error(await Loader(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var down = Error(await Loader(new Handler(_ => throw new HttpRequestException("Connection refused"))).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var thrown = Error(await Loader(new Handler(_ => Answer(Png)), resolvers: [new ThrowingResolver()]).LoadAsync([new AiImageRef { Key = "fss:1" }], default));

        missing.Type.Should().Be(AppErrorType.NotFound);
        missing.Message.Should().Be("The image 'fss:404' could not be read: no image resolver handles that key.");
        notFound.Type.Should().Be(AppErrorType.NotFound);
        notFound.Message.Should().EndWith("the host answered 404.");
        down.Message.Should().EndWith("Connection refused");
        thrown.Type.Should().Be(AppErrorType.NotFound);
        thrown.Message.Should().EndWith("storage offline");
    }

    [Fact]
    public async Task Load_ShouldGiveUp_WhenTheFetchOutlastsItsTimeout()
    {
        var handler = new SlowHandler();

        var error = Error(await Loader(handler, options: new AiModuleOptions { ImageFetchTimeout = TimeSpan.FromMilliseconds(50) })
            .LoadAsync([new AiImageRef { Uri = "https://slow.test/x.png" }], default));

        error.Type.Should().Be(AppErrorType.NotFound);
        error.Message.Should().Contain("the fetch took over");
    }

    [Fact]
    public async Task Load_ShouldRefuseAnImageOverTheSizeLimit_ByItsLengthOrWhileReading()
    {
        var options = new AiModuleOptions { MaxImageBytes = 10 };
        var declared = Error(await Loader(new Handler(_ => Answer(Png)), options: options).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var streamed = Error(await Loader(new Handler(_ => Answer(Png, length: false)), options: options).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var resolved = Error(await Loader(new Handler(_ => Answer(Png)), options, [new KeyResolver("fss:", Png)]).LoadAsync([new AiImageRef { Key = "fss:1" }], default));

        foreach (var error in new[] { declared, streamed, resolved })
        {
            error.Type.Should().Be(AppErrorType.Validation);
            error.Message.Should().EndWith("is over the limit of 10 bytes.");
        }
    }

    [Fact]
    public async Task Load_ShouldRefuseAnUnknownOrDisallowedType_AnEmptyImage_AndAChangedOne()
    {
        var html = Error(await Loader(new Handler(_ => Answer("<html>"u8.ToArray(), "text/html"))).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var gif = Error(await Loader(new Handler(_ => Answer("GIF89a.."u8.ToArray())), new AiModuleOptions { ImageMediaTypes = ["image/png"] })
            .LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var empty = Error(await Loader(new Handler(_ => Answer([]))).LoadAsync([new AiImageRef { Uri = "https://a.test/x" }], default));
        var changed = Error(await Loader(new Handler(_ => Answer(Png))).LoadAsync([new AiImageRef { Uri = "https://a.test/x", Sha256 = new string('0', 64) }], default));
        var same = Value(await Loader(new Handler(_ => Answer(Png))).LoadAsync(
            [new AiImageRef { Uri = "https://a.test/x", Sha256 = Convert.ToHexString(SHA256.HashData(Png)) }], default));

        html.Message.Should().EndWith("is not a JPEG, PNG, WebP or GIF image.");
        gif.Message.Should().Contain("'image/gif', not one of image/png");
        empty.Message.Should().EndWith("is empty.");
        changed.Message.Should().EndWith("no longer matches its SHA-256.");
        new[] { html, gif, empty, changed }.Should().OnlyContain(error => error.Type == AppErrorType.Validation);
        same.Should().ContainSingle("an upper-case SHA-256 matches too");
    }

    [Fact]
    public async Task Load_ShouldRefuseBytesThatAreNoImage_WhateverTheResolverOrTheReferenceSays()
    {
        byte[] unknown = [1, 2, 3, 4];

        var error = Error(await Loader(new Handler(_ => Answer(Png)), resolvers: [new KeyResolver("fss:", unknown, mediaType: "image/png")])
            .LoadAsync([new AiImageRef { Key = "fss:1", MediaType = "image/webp" }], default));

        error.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().Be("The image 'fss:1' is not a JPEG, PNG, WebP or GIF image.");
    }

    [Fact]
    public async Task Load_ShouldRefuseTheFetch_WhenTheHostIsNotListed()
    {
        var handler = new Handler(_ => Answer(Png));
        var options = new AiModuleOptions { ImageFetchHosts = [] };

        var error = Error(await new AiImageLoader([], new Factory(handler), Options.Create(options))
            .LoadAsync([new AiImageRef { Uri = "http://169.254.169.254/latest/meta-data" }], default));

        error.Message.Should().Contain("is not in Ai:Module:ImageFetchHosts");
        handler.Last.Should().BeNull();
    }

    /// <summary>Builds the loader; every test host ends in <c>.test</c>, so the fetch allowlist names <c>*.test</c> unless a test sets its own.</summary>
    private static AiImageLoader Loader(Handler handler, AiModuleOptions? options = null, IEnumerable<IAiImageResolver>? resolvers = null)
    {
        options ??= new AiModuleOptions();
        if (options.ImageFetchHosts.Length == 0)
            options.ImageFetchHosts = ["*.test"];
        return new(resolvers ?? [], new Factory(handler), Options.Create(options));
    }

    private static HttpResponseMessage Answer(byte[] data, string? mediaType = null, bool length = true)
    {
        HttpContent content = length ? new ByteArrayContent(data) : new StreamContent(new MemoryStream(data));
        if (!length)
            content.Headers.ContentLength = null;
        if (mediaType is not null)
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static IReadOnlyList<LoadedImage> Value(Result<IReadOnlyList<LoadedImage>> result) =>
        result.Should().BeOfType<Result<IReadOnlyList<LoadedImage>>.Success>().Subject.Value;

    private static AppError Error(Result<IReadOnlyList<LoadedImage>> result) =>
        result.Should().BeOfType<Result<IReadOnlyList<LoadedImage>>.Failure>().Subject.Error;

    private sealed class KeyResolver(string prefix, byte[] data, string? mediaType = "image/png") : IAiImageResolver
    {
        public int Calls { get; private set; }

        public Task<AiImageContent?> ResolveAsync(AiImageRef image, CancellationToken cancellationToken = default)
        {
            if (image.Key is not { } key || !key.StartsWith(prefix, StringComparison.Ordinal))
                return Task.FromResult<AiImageContent?>(null);
            Calls++;
            return Task.FromResult<AiImageContent?>(new AiImageContent(data, mediaType));
        }
    }

    private sealed class ThrowingResolver : IAiImageResolver
    {
        public Task<AiImageContent?> ResolveAsync(AiImageRef image, CancellationToken cancellationToken = default) =>
            throw new IOException("storage offline");
    }

    private class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public HttpRequestMessage? Last { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            await WaitAsync(cancellationToken);
            return answer(request);
        }

        protected virtual Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SlowHandler() : Handler(_ => Answer(Png))
    {
        protected override Task WaitAsync(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
