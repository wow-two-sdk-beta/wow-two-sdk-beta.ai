using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Runs a use case on the Ai service (<c>POST runs</c>); the service records the call in the caller's data scope.</summary>
/// <param name="transport">The API transport.</param>
internal sealed class ApiAiRunner(AiApiTransport transport) : IAiRunner
{
    /// <inheritdoc />
    public Task<Result<AiRunModel>> RunAsync(AiRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return transport.SendAsync<AiRunModel>(HttpMethod.Post, AiRouteConstants.Runs, request, cancellationToken);
    }
}
