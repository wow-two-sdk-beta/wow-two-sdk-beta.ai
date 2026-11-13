using Microsoft.Extensions.AI;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Providers.Internal;

namespace Ai.Tests.Providers.Support;

/// <summary>Hands out one fake client for every provider and model, and records what it was asked for.</summary>
internal sealed class FakeInnerChatClientFactory(FakeChatClient client) : IAiInnerChatClientFactory
{
    public List<(string Provider, string Kind, string Model)> Created { get; } = [];

    public IChatClient Create(string provider, string kind, AiProviderOptions options, string model)
    {
        Created.Add((provider, kind, model));
        return client;
    }
}
