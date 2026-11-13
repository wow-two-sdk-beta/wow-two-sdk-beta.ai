using Microsoft.Extensions.AI;

namespace Ai.Tests.Providers.Support;

/// <summary>A chat client that records the call and answers with a set response, or throws a set exception.</summary>
internal sealed class FakeChatClient : IChatClient
{
    public ChatResponse Response { get; set; } = new(new ChatMessage(ChatRole.Assistant, "ok"));

    public Exception? Throws { get; set; }

    /// <summary>Runs before the answer, inside the call: advance a fake clock, wait for cancellation.</summary>
    public Func<CancellationToken, Task>? During { get; set; }

    public List<ChatMessage> Messages { get; } = [];

    public ChatOptions? Options { get; private set; }

    public int Calls { get; private set; }

    public bool Disposed { get; private set; }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Messages.AddRange(messages);
        Options = options;
        if (During is not null)
            await During(cancellationToken);
        if (Throws is not null)
            throw Throws;
        return Response;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() => Disposed = true;
}
