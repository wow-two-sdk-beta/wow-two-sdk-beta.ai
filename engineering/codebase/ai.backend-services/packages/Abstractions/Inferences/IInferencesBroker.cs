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
    Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        TotalsAsync(new InferenceTotalsQuery { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);

    /// <summary>Sums the log per use case, provider and model over the query's period and data scopes.</summary>
    /// <param name="query">The period and the scopes to keep.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(InferenceTotalsQuery query, CancellationToken cancellationToken = default);

    /// <summary>Clears the stored outputs of every inference about <paramref name="subject"/>, as a purge of its source requires.</summary>
    /// <param name="subject">The subject reference.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The number of inferences cleared.</returns>
    Task<Result<int>> ForgetSubjectAsync(string subject, CancellationToken cancellationToken = default) =>
        ForgetSubjectAsync(subject, scopes: null, cancellationToken);

    /// <summary>Clears the stored outputs of every inference about <paramref name="subject"/> within the data scopes
    /// named, as a tenant's purge requires.</summary>
    /// <param name="subject">The subject reference.</param>
    /// <param name="scopes">The scopes to clear in, <see cref="AiConstants.Unscoped"/> for calls made without one; none
    /// clears in every scope.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The number of inferences cleared.</returns>
    Task<Result<int>> ForgetSubjectAsync(string subject, IReadOnlyList<string>? scopes, CancellationToken cancellationToken = default);
}
