using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Images;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Ai.Tests.Unit.Images;

/// <summary>The image limits a call or an item meets before anything is resolved, and the media type read from the bytes.</summary>
public sealed class AiImagesTests
{
    private static readonly AiModuleOptions Defaults = new();

    [Fact]
    public void Check_ShouldPass_NoImagesAndWellFormedReferences()
    {
        AiImages.Check(null, Defaults).Should().BeNull();
        AiImages.Check([], Defaults).Should().BeNull();
        AiImages.Check(
        [
            new AiImageRef { Uri = "https://cdn.example.test/poster.jpg", MediaType = "image/JPG" },
            new AiImageRef { Key = "fss:42", Sha256 = new string('A', 64) },
        ], Defaults).Should().BeNull();
    }

    [Fact]
    public void Check_ShouldRefuseMoreImagesThanTheLimit_AndAnyWhenTheLimitIsZero()
    {
        var five = Enumerable.Range(1, 5).Select(index => new AiImageRef { Key = $"fss:{index}" }).ToList();

        var over = AiImages.Check(five, Defaults);
        var none = AiImages.Check([five[0]], new AiModuleOptions { MaxImagesPerCall = 0 });

        over!.Type.Should().Be(AppErrorType.Validation);
        over.Message.Should().Be("A call carries at most 4 images, not 5.");
        none!.Message.Should().Be("This module takes no images.");
    }

    [Theory]
    [InlineData(null, null, null, null, "neither a URI nor a key")]
    [InlineData("https://a.test/x.png", "fss:1", null, null, "both a URI and a key")]
    [InlineData("fss:1", null, null, null, "not an absolute http or https URI")]
    [InlineData("ftp://a.test/x.png", null, null, null, "not an absolute http or https URI")]
    [InlineData("/relative/x.png", null, null, null, "not an absolute http or https URI")]
    [InlineData(null, "fss:1", "image/heic", null, "media type 'image/heic' is not one of")]
    [InlineData(null, "fss:1", null, "abc", "SHA-256 must be 64 hex characters")]
    [InlineData(null, "fss:1", null, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", "SHA-256 must be 64 hex characters")]
    public void Check_ShouldRefuseAMalformedReference(string? uri, string? key, string? mediaType, string? sha256, string problem)
    {
        var error = AiImages.Check([new AiImageRef { Key = "fss:0" }, new AiImageRef { Uri = uri, Key = key, MediaType = mediaType, Sha256 = sha256 }], Defaults);

        error!.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().StartWith("Image 2").And.Contain(problem);
    }

    [Fact]
    public void Check_ShouldRefuseOverlongUrisAndKeys_AndMediaTypesTheModuleLeftOut()
    {
        AiImages.Check([new AiImageRef { Uri = "https://a.test/" + new string('x', 2048) }], Defaults)!.Message.Should().Contain("over 2048 characters");
        AiImages.Check([new AiImageRef { Key = new string('k', 257) }], Defaults)!.Message.Should().Contain("over 256 characters");
        AiImages.Check([new AiImageRef { Key = "fss:1", MediaType = "image/gif" }], new AiModuleOptions { ImageMediaTypes = ["image/png"] })!
            .Message.Should().Contain("is not one of image/png");
    }

    [Theory]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData("IMAGE/JPG", "image/jpeg")]
    [InlineData("image/png; charset=binary", "image/png")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Normalize_ShouldLowerAndStripParameters(string? mediaType, string? expected) =>
        AiImages.Normalize(mediaType).Should().Be(expected);

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 1 }, "image/gif")]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, "image/webp")]
    [InlineData(new byte[] { (byte)'<', (byte)'h', (byte)'t', (byte)'m', (byte)'l', (byte)'>' }, null)]
    [InlineData(new byte[0], null)]
    public void Sniff_ShouldReadTheTypeFromTheFirstBytes(byte[] data, string? expected) =>
        AiImages.Sniff(data).Should().Be(expected);

    [Theory]
    [InlineData(-1, 5 * 1024 * 1024, "MaxImagesPerCall")]
    [InlineData(21, 5 * 1024 * 1024, "MaxImagesPerCall")]
    [InlineData(4, 0, "MaxImageBytes")]
    [InlineData(4, 21 * 1024 * 1024, "MaxImageBytes")]
    public void AddAi_ShouldRefuseImageLimitsOutOfRange(int maxImages, int maxBytes, string option)
    {
        var services = new ServiceCollection();
        services.AddAi().Configure(options =>
        {
            options.ConnectionString = "Host=unused";
            options.MaxImagesPerCall = maxImages;
            options.MaxImageBytes = maxBytes;
        });
        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<IOptions<AiModuleOptions>>().Value;

        read.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain(option);
    }

    [Fact]
    public void AddAi_ShouldRefuseAMediaTypeNoProviderTakes()
    {
        var services = new ServiceCollection();
        services.AddAi().Configure(options =>
        {
            options.ConnectionString = "Host=unused";
            options.ImageMediaTypes = ["image/png", "image/heic"];
        });
        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<IOptions<AiModuleOptions>>().Value;

        read.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("ImageMediaTypes");
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.png", new[] { "cdn.example.com" }, true)]
    [InlineData("https://CDN.example.com/a.png", new[] { "cdn.example.com" }, true)]
    [InlineData("https://img.cdn.example.com/a.png", new[] { "*.cdn.example.com" }, true)]
    [InlineData("https://cdn.example.com/a.png", new[] { "*.cdn.example.com" }, false)]
    [InlineData("https://evil-cdn.example.com/a.png", new[] { "*.cdn.example.com" }, false)]
    [InlineData("http://169.254.169.254/latest/meta-data", new[] { "cdn.example.com" }, false)]
    [InlineData("http://localhost/a.png", new string[0], false)]
    public void FetchAllowed_ShouldAdmitOnlyListedHosts_AndNothingWhenTheListIsEmpty(string uri, string[] hosts, bool allowed) =>
        AiImages.FetchAllowed(new Uri(uri), hosts).Should().Be(allowed);
}
