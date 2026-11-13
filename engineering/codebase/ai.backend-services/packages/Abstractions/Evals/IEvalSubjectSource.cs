namespace WoW.Two.Sdk.Beta.Be.Ai.Evals;

/// <summary>Turns a product's subject into prompt variables, once, when an eval item is added from it.</summary>
/// <remarks>A product registers one per subject prefix (SCFD: <c>harvest:post:</c>); runs never call it again, so the
/// eval bench needs no product code.</remarks>
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
}
