using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Turns a product's subject into prompt variables, and optionally image references, once, when an eval item is
/// added from it.</summary>
/// <remarks>A product registers one per subject prefix (SCFD: <c>harvest:post:</c>); runs never call it again, so the
/// eval bench needs no product code. A source that also returns images overrides <see cref="ResolveInputAsync"/>; one
/// that does not implements <see cref="ResolveAsync"/> alone, as before.</remarks>
public interface IEvalSubjectSource
{
    /// <summary>Gets the subject prefix this source answers, such as <c>harvest:post:</c>.</summary>
    string Prefix { get; }

    /// <summary>Resolves <paramref name="subject"/> to the variables its use case's prompt takes.</summary>
    /// <param name="useCase">The use case the set evaluates.</param>
    /// <param name="subject">The subject reference.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The variables; none when the subject does not exist.</returns>
    Task<IReadOnlyDictionary<string, string?>?> ResolveAsync(string useCase, string subject, CancellationToken cancellationToken = default);

    /// <summary>Resolves <paramref name="subject"/> to the variables and the image references its use case's prompt takes;
    /// the module calls this one. By default it takes <see cref="ResolveAsync"/>'s variables with no images.</summary>
    /// <param name="useCase">The use case the set evaluates.</param>
    /// <param name="subject">The subject reference.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The input; none when the subject does not exist.</returns>
    async Task<EvalSubjectInput?> ResolveInputAsync(string useCase, string subject, CancellationToken cancellationToken = default) =>
        await ResolveAsync(useCase, subject, cancellationToken).ConfigureAwait(false) is { } variables
            ? new EvalSubjectInput { Variables = variables }
            : null;
}

/// <summary>Represents what a subject resolves to: the prompt variables and the image references, in order.</summary>
public sealed record EvalSubjectInput
{
    /// <summary>Gets the prompt variables.</summary>
    public required IReadOnlyDictionary<string, string?> Variables { get; init; }

    /// <summary>Gets the image references; references only, never bytes.</summary>
    public IReadOnlyList<AiImageRef> Images { get; init; } = [];
}
