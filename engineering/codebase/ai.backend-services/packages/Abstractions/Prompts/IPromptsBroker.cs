using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Keeps use cases, their prompts in both modes, and the labels of versioned use cases.</summary>
public interface IPromptsBroker
{
    /// <summary>Creates or replaces a use case.</summary>
    /// <param name="useCase">The use case.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<UseCaseModel>> PutUseCaseAsync(UseCaseModel useCase, CancellationToken cancellationToken = default);

    /// <summary>Reads a use case.</summary>
    /// <param name="key">The use case key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<UseCaseModel>> GetUseCaseAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lists every use case by key.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<UseCaseModel>>> ListUseCasesAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves a prompt: updates the row for the same model match in place under <see cref="PromptMode.Rewrite"/>
    /// (version 1 when new), or inserts the use case's next version under <see cref="PromptMode.Versioned"/>.</summary>
    /// <param name="draft">The prompt.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<PromptModel>> SavePromptAsync(PromptDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Lists a use case's prompt rows, newest version first.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<PromptModel>>> ListPromptsAsync(string useCase, CancellationToken cancellationToken = default);

    /// <summary>Points a label of a versioned use case at a version, keeping the move in the label history.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="label">The label.</param>
    /// <param name="version">The version; it must exist.</param>
    /// <param name="by">Who moved it.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<PromptLabelModel>> MoveLabelAsync(string useCase, string label, int version, string? by = null, CancellationToken cancellationToken = default);

    /// <summary>Lists a use case's labels.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<IReadOnlyList<PromptLabelModel>>> ListLabelsAsync(string useCase, CancellationToken cancellationToken = default);

    /// <summary>Resolves the prompt a call uses for <paramref name="model"/>: the target version is
    /// <paramref name="version"/>, else <paramref name="label"/>'s, else the production label's, else the newest; among rows
    /// at or below it, the most specific model match wins (exact, then the longest prefix, then none), then the highest
    /// version.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="model">The routed model id.</param>
    /// <param name="version">A version to target.</param>
    /// <param name="label">A label to target.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    Task<Result<PromptModel>> ResolveAsync(string useCase, string model, int? version = null, string? label = null, CancellationToken cancellationToken = default);
}
