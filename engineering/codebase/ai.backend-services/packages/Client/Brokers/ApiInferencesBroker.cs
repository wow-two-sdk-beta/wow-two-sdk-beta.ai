using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Reads the inference log on the Ai service, within the data scopes it grants the caller.</summary>
/// <remarks>A filter naming no scope reads every scope the service grants; one naming an empty list reads nothing, with no
/// call.</remarks>
/// <param name="transport">The API transport.</param>
internal sealed class ApiInferencesBroker(AiApiTransport transport) : IInferencesBroker
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<InferenceModel>>> ListAsync(InferenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Scopes is { Count: 0 })
            return Task.FromResult(Result<IReadOnlyList<InferenceModel>>.Ok([]));

        var path = AiApiTransport.Path(AiRouteConstants.Inferences, [
            ("useCase", query.UseCase), ("subject", query.Subject), ("evalRunId", query.EvalRunId?.ToString()),
            ("from", AiApiTransport.Instant(query.FromUtc)), ("to", AiApiTransport.Instant(query.ToUtc)),
            ("limit", query.Limit.ToString(CultureInfo.InvariantCulture)), .. AiApiTransport.Scopes(query.Scopes)]);
        return transport.GetAsync<IReadOnlyList<InferenceModel>>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        TotalsAsync(new InferenceTotalsQuery { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<InferenceTotals>>> TotalsAsync(InferenceTotalsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Scopes is { Count: 0 })
            return Task.FromResult(Result<IReadOnlyList<InferenceTotals>>.Ok([]));

        var path = AiApiTransport.Path(AiRouteConstants.InferenceTotals, [
            ("from", AiApiTransport.Instant(query.FromUtc)), ("to", AiApiTransport.Instant(query.ToUtc)), .. AiApiTransport.Scopes(query.Scopes)]);
        return transport.GetAsync<IReadOnlyList<InferenceTotals>>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<int>> ForgetSubjectAsync(string subject, CancellationToken cancellationToken = default) =>
        ForgetSubjectAsync(subject, scopes: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<int>> ForgetSubjectAsync(string subject, IReadOnlyList<string>? scopes, CancellationToken cancellationToken = default)
    {
        if (scopes is { Count: 0 })
            return Task.FromResult(Result<int>.Ok(0));

        var path = AiApiTransport.Path(AiRouteConstants.InferenceForget, [.. AiApiTransport.Scopes(scopes)]);
        return transport.SendAsync<int>(HttpMethod.Post, path, new Forget(subject ?? string.Empty), cancellationToken);
    }

    /// <summary>Represents the body of a purge.</summary>
    private sealed record Forget(string Subject);
}
