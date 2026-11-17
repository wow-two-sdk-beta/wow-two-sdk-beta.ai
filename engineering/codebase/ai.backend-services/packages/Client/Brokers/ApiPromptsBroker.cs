using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Client.Brokers;

/// <summary>Keeps use cases, prompts and labels on the Ai service.</summary>
/// <param name="transport">The API transport.</param>
internal sealed class ApiPromptsBroker(AiApiTransport transport) : IPromptsBroker
{
    /// <inheritdoc />
    public Task<Result<UseCaseModel>> PutUseCaseAsync(UseCaseModel useCase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        return AiApiTransport.Blank(useCase.Key, "use case key") is { } error
            ? Task.FromResult(Result<UseCaseModel>.Fail(error))
            : transport.SendAsync<UseCaseModel>(HttpMethod.Put, UseCase(useCase.Key), useCase, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<UseCaseModel>> GetUseCaseAsync(string key, CancellationToken cancellationToken = default) =>
        AiApiTransport.Blank(key, "use case key") is { } error
            ? Task.FromResult(Result<UseCaseModel>.Fail(error))
            : transport.GetAsync<UseCaseModel>(UseCase(key), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<UseCaseModel>>> ListUseCasesAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<IReadOnlyList<UseCaseModel>>(AiRouteConstants.UseCases, cancellationToken);

    /// <inheritdoc />
    public Task<Result<PromptModel>> SavePromptAsync(PromptDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AiApiTransport.Blank(draft.UseCase, "use case key") is { } error
            ? Task.FromResult(Result<PromptModel>.Fail(error))
            : transport.SendAsync<PromptModel>(HttpMethod.Post, AiRouteConstants.Fill(AiRouteConstants.Prompts, ("key", draft.UseCase)), draft, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<PromptModel>>> ListPromptsAsync(string useCase, CancellationToken cancellationToken = default) =>
        AiApiTransport.Blank(useCase, "use case key") is { } error
            ? Task.FromResult(Result<IReadOnlyList<PromptModel>>.Fail(error))
            : transport.GetAsync<IReadOnlyList<PromptModel>>(AiRouteConstants.Fill(AiRouteConstants.Prompts, ("key", useCase)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<PromptLabelModel>> MoveLabelAsync(string useCase, string label, int version, string? by = null, CancellationToken cancellationToken = default)
    {
        if ((AiApiTransport.Blank(useCase, "use case key") ?? AiApiTransport.Blank(label, "label")) is { } error)
            return Task.FromResult(Result<PromptLabelModel>.Fail(error));

        var path = AiRouteConstants.Fill(AiRouteConstants.Label, ("key", useCase), ("label", label.Trim()));
        return transport.SendAsync<PromptLabelModel>(HttpMethod.Put, path, new LabelMove(version, by), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<PromptLabelModel>>> ListLabelsAsync(string useCase, CancellationToken cancellationToken = default) =>
        AiApiTransport.Blank(useCase, "use case key") is { } error
            ? Task.FromResult(Result<IReadOnlyList<PromptLabelModel>>.Fail(error))
            : transport.GetAsync<IReadOnlyList<PromptLabelModel>>(AiRouteConstants.Fill(AiRouteConstants.Labels, ("key", useCase)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<PromptModel>> ResolveAsync(string useCase, string model, int? version = null, string? label = null, CancellationToken cancellationToken = default)
    {
        if ((AiApiTransport.Blank(useCase, "use case key") ?? AiApiTransport.Blank(model, "model")) is { } error)
            return Task.FromResult(Result<PromptModel>.Fail(error));

        var path = AiApiTransport.Path(AiRouteConstants.Fill(AiRouteConstants.PromptResolve, ("key", useCase)),
            ("model", model), ("version", version?.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("label", label));
        return transport.GetAsync<PromptModel>(path, cancellationToken);
    }

    private static string UseCase(string key) => AiRouteConstants.Fill(AiRouteConstants.UseCase, ("key", key));

    /// <summary>Represents the body of a label move.</summary>
    private sealed record LabelMove(int Version, string? By);
}
