using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;

/// <summary>Runs a use case in process: resolves and renders its prompt for every routed model before any call, answers
/// from a stored inference when one matches, else calls each model in turn with retries on transient failures, checks
/// the answer against the output schema, and logs every attempt with its tokens and cost.</summary>
/// <param name="db">The module's store.</param>
/// <param name="caller">The provider seam, one attempt per call.</param>
/// <param name="options">The module's settings: retries, backoff and the default output budget.</param>
/// <param name="time">The clock backoff waits on.</param>
internal sealed class ModuleAiRunner(AiDbContext db, IAiModelCaller caller, IOptions<AiModuleOptions> options, TimeProvider time) : IAiRunner
{
    /// <summary>Holds the longest error message an inference keeps.</summary>
    private const int MaxErrorLength = 500;

    /// <inheritdoc />
    public async Task<Result<AiRunModel>> RunAsync(AiRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Subject is { Length: > AiConstants.MaxSubjectLength })
            return Result<AiRunModel>.Fail(AiErrors.Invalid($"A subject is at most {AiConstants.MaxSubjectLength} characters."));

        var loaded = await PromptBook.LoadAsync(db, request.UseCase, cancellationToken);
        if (loaded is Result<PromptBook>.Failure { Error: var loadError })
            return Result<AiRunModel>.Fail(loadError);

        var book = ((Result<PromptBook>.Success)loaded).Value;
        var planned = Plan(book, request);
        if (planned is Result<IReadOnlyList<AttemptPlan>>.Failure { Error: var planError })
            return Result<AiRunModel>.Fail(planError);

        var plans = ((Result<IReadOnlyList<AttemptPlan>>.Success)planned).Value;
        var settings = options.Value;
        var prices = await LoadPricesAsync(plans, cancellationToken);
        var attempts = new List<InferenceModel>();
        foreach (var plan in plans)
        {
            if (!request.Fresh && await FindStoredAsync(book.UseCase.Key, plan, cancellationToken) is { } stored)
            {
                attempts.Add(stored);
                return Result<AiRunModel>.Ok(new AiRunModel { Outcome = AiOutcome.Succeeded, Output = stored.Output, Attempts = attempts, FromCache = true });
            }

            for (var retry = 0; ; retry++)
            {
                var (inference, answer) = await AttemptAsync(book.UseCase, plan, attempts.Count + 1, request, prices, cancellationToken);
                attempts.Add(inference);
                if (inference.Outcome == AiOutcome.Succeeded)
                    return Result<AiRunModel>.Ok(new AiRunModel { Outcome = AiOutcome.Succeeded, Output = answer, Attempts = attempts });

                if (inference.Outcome != AiOutcome.Failed || inference.ErrorKind is not { } kind || !AiRetry.IsTransient(kind) || retry >= settings.RetriesPerModel)
                    break;

                await WaitAsync(Backoff(settings.RetryDelay, retry), cancellationToken);
            }
        }

        return Result<AiRunModel>.Ok(new AiRunModel { Outcome = attempts[^1].Outcome, Attempts = attempts });
    }

    /// <summary>Gets the wait before retry <paramref name="retry"/> (from 0): the base delay doubled per retry.</summary>
    /// <param name="baseDelay">The first retry's delay.</param>
    /// <param name="retry">The retry's position, from 0.</param>
    internal static TimeSpan Backoff(TimeSpan baseDelay, int retry) =>
        baseDelay <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(baseDelay.Ticks * (1L << Math.Min(retry, 20)));

    /// <summary>Resolves and renders the prompt of every routed model that has one, failing before any call on a bad
    /// route, a missing label, no prompt for any model, or a missing variable.</summary>
    private Result<IReadOnlyList<AttemptPlan>> Plan(PromptBook book, AiRunRequest request)
    {
        var entries = request.Models is { Count: > 0 } models ? models : book.UseCase.Route;
        if (entries.Count == 0)
            return Result<IReadOnlyList<AttemptPlan>>.Fail(AiErrors.RouteEmpty(book.UseCase.Key));

        var references = new List<AiModelRef>();
        foreach (var entry in entries)
        {
            if (!AiModelRef.TryParse(entry, out var reference))
                return Result<IReadOnlyList<AttemptPlan>>.Fail(AiErrors.Invalid($"The model reference '{entry}' is not {{provider}}/{{model}}."));
            references.Add(reference!);
        }

        var target = book.Target(request.Version, request.Label);
        if (target is Result<int>.Failure { Error: var targetError })
            return Result<IReadOnlyList<AttemptPlan>>.Fail(targetError);

        var version = ((Result<int>.Success)target).Value;
        var defaultMaxTokens = options.Value.DefaultMaxOutputTokens;
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var plans = new List<AttemptPlan>();
        foreach (var reference in references)
        {
            if (ModelMatch.Pick(book.Rows, reference.Model, version) is not { } prompt)
                continue;

            var parameters = prompt.Parameters with { MaxOutputTokens = prompt.Parameters.MaxOutputTokens ?? defaultMaxTokens };
            var system = PromptTemplates.Render(prompt.SystemTemplate, request.Variables, missing);
            var user = PromptTemplates.Render(prompt.UserTemplate, request.Variables, missing);
            var inputHash = AiHashes.Input(system, user, book.UseCase.OutputSchema, reference.ToString(), parameters);
            plans.Add(new AttemptPlan(reference, prompt, system, user, parameters, inputHash));
        }

        if (plans.Count == 0)
            return Result<IReadOnlyList<AttemptPlan>>.Fail(AiErrors.PromptNotFound(book.UseCase.Key, references[0].ToString()));
        if (missing.Count > 0)
            return Result<IReadOnlyList<AttemptPlan>>.Fail(AiErrors.Invalid(
                $"The prompt of AI use case '{book.UseCase.Key}' needs {(missing.Count == 1 ? "a variable" : "variables")} it was not given: {string.Join(", ", missing)}."));

        return Result<IReadOnlyList<AttemptPlan>>.Ok(plans);
    }

    /// <summary>Reads the price rows of every planned model.</summary>
    private async Task<IReadOnlyList<ModelPriceModel>> LoadPricesAsync(IReadOnlyList<AttemptPlan> plans, CancellationToken cancellationToken)
    {
        var models = plans.Select(plan => plan.Model.Model).Distinct().ToArray();
        var rows = await db.ModelPrices.AsNoTracking().Where(entity => models.Contains(entity.Model)).ToListAsync(cancellationToken);
        return [.. rows.Select(AiModels.ToModel)];
    }

    /// <summary>Finds the newest stored answer for the same use case, prompt text, model and rendered input.</summary>
    private async Task<InferenceModel?> FindStoredAsync(string useCase, AttemptPlan plan, CancellationToken cancellationToken)
    {
        var succeeded = AiLabels.Outcome(AiOutcome.Succeeded);
        var row = await db.Inferences.AsNoTracking()
            .Where(entity => entity.UseCase == useCase
                && entity.PromptHash == plan.Prompt.ContentHash
                && entity.Provider == plan.Model.Provider
                && entity.Model == plan.Model.Model
                && entity.InputHash == plan.InputHash
                && entity.Outcome == succeeded
                && entity.Output != null)
            .OrderByDescending(entity => entity.At)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : AiModels.ToModel(row);
    }

    /// <summary>Calls the model once, classifies the result, prices it, sets the telemetry tags and logs it.</summary>
    private async Task<(InferenceModel Inference, string? Answer)> AttemptAsync(
        UseCaseModel useCase, AttemptPlan plan, int attempt, AiRunRequest request, IReadOnlyList<ModelPriceModel> prices, CancellationToken cancellationToken)
    {
        using var activity = AiModuleTelemetry.Source.StartActivity($"chat {plan.Model.Model}", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "chat");
        activity?.SetTag("gen_ai.provider.name", plan.Model.Provider);
        activity?.SetTag("gen_ai.request.model", plan.Model.Model);
        activity?.SetTag("gen_ai.prompt.name", useCase.Key);
        activity?.SetTag("gen_ai.prompt.version", plan.Prompt.Version);

        var result = await CallAsync(useCase, plan, cancellationToken);
        var (outcome, kind, error, answer) = Classify(result, useCase.OutputSchema);
        var at = StoredClock.Truncate(result.StartedAtUtc);
        var price = PriceMath.InEffect(prices.Where(row => row.Provider == plan.Model.Provider && row.Model == plan.Model.Model), at);
        var cost = price is null ? 0 : PriceMath.Cost(price, result.InputTokens, result.OutputTokens, result.CacheReadTokens, result.CacheWriteTokens);

        activity?.SetTag("gen_ai.usage.input_tokens", result.InputTokens);
        activity?.SetTag("gen_ai.usage.output_tokens", result.OutputTokens);
        activity?.SetTag("ai.outcome", AiLabels.Outcome(outcome));
        if (kind is { } errorKind)
        {
            activity?.SetTag("error.type", AiLabels.Write(errorKind));
            activity?.SetStatus(ActivityStatusCode.Error, error);
        }

        var row = new InferenceEntity
        {
            Id = Guid.CreateVersion7(),
            At = at,
            UseCase = useCase.Key,
            PromptVersion = plan.Prompt.Version,
            PromptHash = plan.Prompt.ContentHash,
            PromptModelMatch = plan.Prompt.ModelMatch,
            Provider = plan.Model.Provider,
            Model = plan.Model.Model,
            Attempt = attempt,
            Subject = request.Subject,
            InputHash = plan.InputHash,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            CacheReadTokens = result.CacheReadTokens,
            CacheWriteTokens = result.CacheWriteTokens,
            ReasoningTokens = result.ReasoningTokens,
            CostMicroUsd = cost,
            Priced = price is not null,
            LatencyMs = Math.Max(0, result.LatencyMs),
            Outcome = AiLabels.Outcome(outcome),
            ErrorKind = AiLabels.ErrorKind(kind),
            Error = Trim(error),
            FinishReason = Trim(result.FinishReason, 64),
            Output = useCase.KeepOutputs ? (outcome == AiOutcome.Succeeded ? answer : result.Text) : null,
            EvalRunId = request.EvalRunId,
        };
        db.Inferences.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(row).State = EntityState.Detached;
        return (AiModels.ToModel(row), answer);
    }

    /// <summary>Calls the provider seam; an exception it should not throw becomes an <see cref="AiErrorKind.Unknown"/> attempt.</summary>
    private async Task<AiCallResult> CallAsync(UseCaseModel useCase, AttemptPlan plan, CancellationToken cancellationToken)
    {
        var started = time.GetUtcNow();
        var watch = Stopwatch.StartNew();
        try
        {
            return await caller.CallAsync(new AiCallRequest
            {
                Model = plan.Model,
                System = plan.System,
                User = plan.User,
                JsonSchema = useCase.OutputSchema,
                SchemaName = useCase.Key,
                Temperature = plan.Parameters.Temperature,
                MaxOutputTokens = plan.Parameters.MaxOutputTokens ?? options.Value.DefaultMaxOutputTokens,
                Effort = plan.Parameters.Effort,
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new AiCallResult
            {
                Model = plan.Model,
                StartedAtUtc = started,
                LatencyMs = watch.ElapsedMilliseconds,
                ErrorKind = AiErrorKind.Unknown,
                Error = exception.Message,
            };
        }
    }

    /// <summary>Classifies an attempt: a filtered or refused answer is refused; a provider error fails; an answer that
    /// misses the schema is schema-invalid; anything else succeeded, with the answer (fence stripped under a schema).</summary>
    /// <param name="result">The attempt's result.</param>
    /// <param name="schema">The use case's output schema.</param>
    internal static (AiOutcome Outcome, AiErrorKind? Kind, string? Error, string? Answer) Classify(AiCallResult result, string? schema)
    {
        if (result.ErrorKind == AiErrorKind.ContentFiltered)
            return (AiOutcome.Refused, AiErrorKind.ContentFiltered, result.Error, null);
        if (result.ErrorKind == AiErrorKind.SchemaMismatch)
            return (AiOutcome.SchemaInvalid, AiErrorKind.SchemaMismatch, result.Error, null);
        if (result.ErrorKind is { } kind)
            return (AiOutcome.Failed, kind, result.Error, null);
        if (result.Text is null)
            return (AiOutcome.Failed, AiErrorKind.InvalidResponse, result.Error ?? "The provider answered without text.", null);
        if (result.FinishReason is { } reason && (reason.Equals("refusal", StringComparison.OrdinalIgnoreCase) || reason.Equals("content_filter", StringComparison.OrdinalIgnoreCase)))
            return (AiOutcome.Refused, AiErrorKind.ContentFiltered, $"The model stopped with '{reason}'.", null);
        if (schema is null)
            return (AiOutcome.Succeeded, null, null, result.Text);

        return OutputSchemaCheck.TryCheck(result.Text, schema, out var json, out var error)
            ? (AiOutcome.Succeeded, null, null, json)
            : (AiOutcome.SchemaInvalid, AiErrorKind.SchemaMismatch, error, null);
    }

    private Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, time, cancellationToken);

    private static string? Trim(string? text, int length = MaxErrorLength) =>
        text is null ? null : text.Length <= length ? text : text[..length];

    /// <summary>Holds one routed model's resolved and rendered prompt.</summary>
    private sealed record AttemptPlan(AiModelRef Model, PromptModel Prompt, string System, string User, PromptParameters Parameters, string InputHash);
}
