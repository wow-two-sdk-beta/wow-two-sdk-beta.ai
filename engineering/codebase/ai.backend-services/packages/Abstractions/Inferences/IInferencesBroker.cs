using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Inferences;

/// <summary>Reads the inference log: every model attempt with its prompt version, tokens, cost and latency.</summary>
public interface IInferencesBroker
{
    /// <summary>Lists inferences matching <paramref name="query"/>, newest first.</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<InferenceModel>>> ListAsync(InferenceQuery query, CancellationToken cancellationToken = default);

    /// <summary>Sums the log per use case, provider and model between two times.</summary>
    /// <param name="fromUtc">The start (UTC, inclusive).</param>
    /// <param name="toUtc">The end (UTC, exclusive).</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>Clears the stored outputs of every inference about <paramref name="subject"/>, as a purge of its source requires.</summary>
    /// <param name="subject">The subject reference.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The number of inferences cleared.</returns>
    Task<Result<int>> ForgetSubjectAsync(string subject, CancellationToken cancellationToken = default);
}
