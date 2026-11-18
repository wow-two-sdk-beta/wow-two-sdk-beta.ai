using System.Net;
using System.Text;

namespace Ai.Tests.Providers.Support;

/// <summary>An HTTP handler that records each request and answers with a set status and body, or throws; <see cref="Respond"/>
/// answers each request by its body instead, to play a model that accepts some requests and refuses others.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public Exception? Throws { get; init; }

    /// <summary>Answers a request from its body; takes the place of the set status and body.</summary>
    public Func<string, (HttpStatusCode Status, string Body)>? Respond { get; init; }

    /// <summary>The body of every request, in order.</summary>
    public List<string> Bodies { get; } = [];

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Bodies.Add(LastBody ?? string.Empty);
        if (Throws is not null)
            throw Throws;
        var (answerStatus, answerBody) = Respond is null ? (status, body) : Respond(LastBody ?? string.Empty);
        return new HttpResponseMessage(answerStatus)
        {
            Content = new StringContent(answerBody, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
    }
}
