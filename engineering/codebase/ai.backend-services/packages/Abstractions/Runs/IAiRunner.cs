using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Runs;

/// <summary>Runs a use case: resolves its prompt for each routed model, calls the model, checks the answer against the
/// output schema, falls back along the route, and records every attempt with its tokens and cost.</summary>
public interface IAiRunner
{
    /// <summary>Runs <paramref name="request"/>.</summary>
    /// <param name="request">The use case, its variables and what to record the call against.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run, whatever its outcome; a failure only when the run cannot start: an unknown use case, an empty
    /// route, or no prompt for any routed model.</returns>
    Task<Result<AiRunModel>> RunAsync(AiRunRequest request, CancellationToken cancellationToken = default);
}
