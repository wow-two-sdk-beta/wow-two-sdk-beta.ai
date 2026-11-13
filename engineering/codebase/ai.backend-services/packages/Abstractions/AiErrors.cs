using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Ai;

/// <summary>Holds the error catalogue of the AI contract.</summary>
/// <remarks>A model call that fails is not an error: <see cref="Runs.IAiRunner.RunAsync"/> answers it as an outcome with
/// its attempts, so the caller keeps the tokens and cost. These errors cover requests the layer cannot start.</remarks>
public static class AiErrors
{
    /// <summary>Creates the error for a use case that does not exist.</summary>
    /// <param name="useCase">The use case key.</param>
    public static AppError UseCaseNotFound(string useCase) => AppErrorFactory.NotFound($"AI use case '{useCase}' does not exist.");

    /// <summary>Creates the error for a use case with no prompt for the routed model.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="model">The model the prompt was resolved for.</param>
    public static AppError PromptNotFound(string useCase, string model) =>
        AppErrorFactory.NotFound($"AI use case '{useCase}' has no prompt for model '{model}'.");

    /// <summary>Creates the error for a use case whose route names no model.</summary>
    /// <param name="useCase">The use case key.</param>
    public static AppError RouteEmpty(string useCase) => AppErrorFactory.Validation($"AI use case '{useCase}' has no model route.");

    /// <summary>Creates the error for an eval set that does not exist.</summary>
    /// <param name="set">The eval set key.</param>
    public static AppError EvalSetNotFound(string set) => AppErrorFactory.NotFound($"Eval set '{set}' does not exist.");

    /// <summary>Creates the error for an eval run that does not exist.</summary>
    /// <param name="runId">The run id.</param>
    public static AppError EvalRunNotFound(Guid runId) => AppErrorFactory.NotFound($"Eval run '{runId}' does not exist.");

    /// <summary>Creates the error for a label a versioned use case does not have.</summary>
    /// <param name="useCase">The use case key.</param>
    /// <param name="label">The label.</param>
    public static AppError LabelNotFound(string useCase, string label) =>
        AppErrorFactory.NotFound($"AI use case '{useCase}' has no label '{label}'.");

    /// <summary>Creates the error for an eval item that does not exist.</summary>
    /// <param name="itemId">The item id.</param>
    public static AppError EvalItemNotFound(Guid itemId) => AppErrorFactory.NotFound($"Eval item '{itemId}' does not exist.");

    /// <summary>Creates the error for an item a run did not score.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="itemId">The item id.</param>
    public static AppError EvalScoreNotFound(Guid runId, Guid itemId) =>
        AppErrorFactory.NotFound($"Eval run '{runId}' did not score item '{itemId}'.");

    /// <summary>Creates the error for a subject no registered <see cref="Evals.IEvalSubjectSource"/> resolves.</summary>
    /// <param name="subject">The subject reference.</param>
    public static AppError SubjectNotResolved(string subject) => AppErrorFactory.NotFound($"No subject source resolves '{subject}'.");

    /// <summary>Creates a validation error.</summary>
    /// <param name="message">What is wrong.</param>
    public static AppError Invalid(string message) => AppErrorFactory.Validation(message);
}
