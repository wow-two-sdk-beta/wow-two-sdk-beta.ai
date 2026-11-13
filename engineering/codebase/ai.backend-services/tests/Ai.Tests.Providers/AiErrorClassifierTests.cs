using System.ClientModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Anthropic.Exceptions;
using AwesomeAssertions;
using OllamaSharp.Models.Exceptions;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Providers;

/// <summary>The classification table: statuses and exceptions onto <see cref="AiErrorKind"/>, and error trimming.</summary>
public sealed class AiErrorClassifierTests
{
    [Theory]
    [InlineData(400, AiErrorKind.BadRequest)]
    [InlineData(401, AiErrorKind.Unauthorized)]
    [InlineData(403, AiErrorKind.Unauthorized)]
    [InlineData(404, AiErrorKind.BadRequest)]
    [InlineData(408, AiErrorKind.Timeout)]
    [InlineData(413, AiErrorKind.BadRequest)]
    [InlineData(422, AiErrorKind.BadRequest)]
    [InlineData(429, AiErrorKind.RateLimited)]
    [InlineData(500, AiErrorKind.Unavailable)]
    [InlineData(503, AiErrorKind.Unavailable)]
    [InlineData(529, AiErrorKind.Unavailable)]
    [InlineData(302, AiErrorKind.Unknown)]
    public void ClassifyStatus_ShouldMapTheStatus(int status, AiErrorKind kind) =>
        AiErrorClassifier.ClassifyStatus(status).Should().Be(kind);

    public static TheoryData<Exception, AiErrorKind> Exceptions() => new()
    {
        { new AnthropicApiException("x") { StatusCode = (HttpStatusCode)529, ResponseBody = "{}" }, AiErrorKind.Unavailable },
        { new AnthropicApiException("x") { StatusCode = HttpStatusCode.TooManyRequests, ResponseBody = "{}" }, AiErrorKind.RateLimited },
        { new AnthropicIOException("connection refused", new HttpRequestException("refused")), AiErrorKind.Network },
        { new AnthropicInvalidDataException("bad body"), AiErrorKind.InvalidResponse },
        { new ClientResultException("no response", null, new HttpRequestException("refused")), AiErrorKind.Network },
        { new HttpRequestException("x", null, HttpStatusCode.Forbidden), AiErrorKind.Unauthorized },
        { new HttpRequestException("x", null, HttpStatusCode.BadGateway), AiErrorKind.Unavailable },
        { new HttpRequestException("connection refused"), AiErrorKind.Network },
        { new SocketException((int)SocketError.ConnectionRefused), AiErrorKind.Network },
        { new OllamaException("model 'x' not found"), AiErrorKind.BadRequest },
        { new ResponseError("runner crashed"), AiErrorKind.Unknown },
        { new TimeoutException(), AiErrorKind.Timeout },
        { new TaskCanceledException("timed out", new TimeoutException()), AiErrorKind.Timeout },
        { new JsonException("bad json"), AiErrorKind.InvalidResponse },
        { new InvalidOperationException("wrapped", new HttpRequestException("refused")), AiErrorKind.Network },
        { new InvalidOperationException("odd"), AiErrorKind.Unknown },
    };

    [Theory]
    [MemberData(nameof(Exceptions))]
    public void Classify_ShouldMapTheException(Exception exception, AiErrorKind kind) =>
        AiErrorClassifier.Classify(exception).Should().Be(kind);

    [Fact]
    public void Describe_ShouldUseTheAnthropicResponseBody()
    {
        var exception = new AnthropicApiException("x")
        {
            StatusCode = (HttpStatusCode)529,
            ResponseBody = """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""",
        };

        AiErrorClassifier.Describe(exception).Should().StartWith("529: ").And.Contain("overloaded_error");
    }

    [Fact]
    public void Sanitize_ShouldDropTheKey_FoldWhitespace_AndTrimTo500()
    {
        var message = "Bad key secret-123\n\n  in   request " + new string('y', 1000);

        var error = AiErrorClassifier.Sanitize(message, "secret-123");

        error.Should().StartWith("Bad key *** in request yyy").And.NotContain("secret-123");
        error.Length.Should().Be(500);
        error.Should().EndWith("…");
    }

    [Fact]
    public void Sanitize_ShouldKeepAShortMessageWhole() =>
        AiErrorClassifier.Sanitize(" short ", null).Should().Be("short");
}
