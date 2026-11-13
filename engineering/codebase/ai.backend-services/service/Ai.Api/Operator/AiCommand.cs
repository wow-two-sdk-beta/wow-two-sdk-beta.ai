using System.Text.Json;
using Ai.Api.Configurations;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Ai.Api.Operator;

/// <summary>Answers the <c>ai</c> operator verbs: use cases, prompts, labels, prices, runs, the inference log and evals,
/// over the service's database, before any host boots — the eval bench Claude Code drives.</summary>
/// <remarks>
///   - composes the module as the host does (<c>AddAiProviders</c> beside <c>AddAi</c>) over the configured database,
///     already migrated by the deploy's or a local run's <c>migrate</c>; provider keys come from the configuration
///     (user-secrets on the local bench)
///   - prints one JSON line per verb (<see cref="AiOperatorWire"/>); <c>eval report --format md|csv</c> prints the
///     table instead
///   - files the verbs read (<c>--system-file</c>, <c>--schema-file</c>, <c>--input-file</c>, <c>--file</c>) are inputs
///     only: prompts, sets and items live in the tables
/// </remarks>
public static class AiCommand
{
    /// <summary>The verb's name, the first process argument.</summary>
    public const string Verb = "ai";

    /// <summary>Runs a verb against the host's own configuration.</summary>
    /// <param name="args">The arguments after <c>ai</c>.</param>
    /// <param name="output">Where the result line goes.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The exit code: 0 done, 1 refused, 2 failed.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        // A usage error needs no settings, so it never builds the host's configuration.
        if (!AiArguments.TryParse(args, out _, out _))
            return await RunAsync(args, new ConfigurationBuilder().Build(), output, configureServices: null, cancellationToken);

        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync(args, builder.Configuration, output, configureServices: null, cancellationToken);
    }

    /// <summary>Runs a verb against a given configuration.</summary>
    /// <param name="args">The arguments after <c>ai</c>.</param>
    /// <param name="configuration">The settings: the database, the providers and <c>Ai:Module</c>.</param>
    /// <param name="output">Where the result line goes.</param>
    /// <param name="configureServices">Changes the composed services after the module, such as a scripted model caller
    /// in tests.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The exit code: 0 done, 1 refused, 2 failed.</returns>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, IConfiguration configuration, TextWriter output, Action<IServiceCollection>? configureServices = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(output);

        if (!AiArguments.TryParse(args, out var arguments, out var problem))
        {
            var command = string.Join(' ', args.TakeWhile(arg => !arg.StartsWith("--", StringComparison.Ordinal)));
            var usage = AiArguments.Specs.TryGetValue(command, out var spec)
                ? spec.Usage(command)
                : string.Join(" · ", AiArguments.Specs.Keys.Select(key => $"ai {key}"));
            return await AiOperatorWire.ErrorAsync(output, command.Length == 0 ? null : command, "usage", $"{problem}; usage: {usage}");
        }

        var connectionString = AiOperatorWire.ConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
            return await AiOperatorWire.ErrorAsync(output, arguments.Command, "no-database", "Set DB_CONNECTION or DatabaseSettings:ConnectionString.", AiOperatorWire.Failed);

        var services = new ServiceCollection().AddLogging();
        services.AddAiProviders(configuration);
        services.AddAi(AiModuleConstants.ServiceSchemaOwner).Configure(options => HostConfiguration.BindModule(options, configuration, connectionString));
        configureServices?.Invoke(services);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        try
        {
            return await new Handlers(scope.ServiceProvider, arguments, output).RunAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or OptionsValidationException or IOException or UnauthorizedAccessException or JsonException
            || exception.InnerException is NpgsqlException)
        {
            var message = exception is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable }
                ? $"{exception.Message} — run `migrate` first."
                : (exception.InnerException?.Message ?? exception.Message);
            return await AiOperatorWire.ErrorAsync(output, arguments.Command, exception is JsonException ? "not-json" : "failed", message, AiOperatorWire.Failed);
        }
    }

    /// <summary>Runs one parsed command over the composed services.</summary>
    private sealed class Handlers(IServiceProvider services, AiArguments args, TextWriter output)
    {
        private string Command => args.Command;

        public Task<int> RunAsync(CancellationToken ct) => Command switch
        {
            "usecase put" => PutUseCaseAsync(ct),
            "usecase list" => ReplyAsync(Prompts.ListUseCasesAsync(ct)),
            "prompt save" => SavePromptAsync(ct),
            "prompt list" => ListPromptsAsync(ct),
            "label move" => MoveLabelAsync(ct),
            "price put" => PutPriceAsync(ct),
            "price list" => ReplyAsync(Prices.ListAsync(ct)),
            "run" => RunUseCaseAsync(ct),
            "inferences" => ListInferencesAsync(ct),
            "totals" => TotalsAsync(ct),
            "eval set put" => PutSetAsync(ct),
            "eval sets" => ReplyAsync(Evals.ListSetsAsync(ct)),
            "eval add" => AddItemAsync(ct),
            "eval relabel" => RelabelAsync(ct),
            "eval remove" => RemoveAsync(ct),
            "eval items" => ReplyAsync(Evals.ListItemsAsync(args.Value("set")!, ct)),
            "eval run" => RunEvalAsync(ct),
            "eval runs" => ReplyAsync(Evals.ListRunsAsync(args.Value("set")!, ct)),
            "eval report" => ReportAsync(ct),
            "eval rate" => RateAsync(ct),
            "eval export" => ExportAsync(ct),
            "eval import" => ImportAsync(ct),
            _ => Usage($"'{Command}' has no handler"),
        };

        private IPromptsBroker Prompts => services.GetRequiredService<IPromptsBroker>();

        private IModelPricesBroker Prices => services.GetRequiredService<IModelPricesBroker>();

        private IInferencesBroker Inferences => services.GetRequiredService<IInferencesBroker>();

        private IEvalsBroker Evals => services.GetRequiredService<IEvalsBroker>();

        private async Task<int> PutUseCaseAsync(CancellationToken ct)
        {
            var key = args.Value("key")!;
            if (!Enum.TryParse<PromptMode>(args.Value("mode"), ignoreCase: true, out var mode) || !Enum.IsDefined(mode))
                return await Usage("'--mode' is rewrite or versioned");
            if (!args.TryBool("keep-outputs", out var keepOutputs))
                return await Usage("'--keep-outputs' is true or false");

            // Options left out keep the stored values, so a put can change the route alone.
            var existing = await Prompts.GetUseCaseAsync(key, ct) is Result<UseCaseModel>.Success { Value: var found } ? found : null;
            var schema = args.Value("schema-file") is { } schemaFile ? await File.ReadAllTextAsync(schemaFile, ct) : existing?.OutputSchema;
            return await ReplyAsync(Prompts.PutUseCaseAsync(new UseCaseModel
            {
                Key = key,
                Description = args.Value("description") ?? existing?.Description,
                PromptMode = mode,
                OutputSchema = schema,
                Route = args.List("route"),
                KeepOutputs = keepOutputs ?? existing?.KeepOutputs ?? true,
            }, ct));
        }

        private async Task<int> SavePromptAsync(CancellationToken ct)
        {
            if (!args.TryDouble("temperature", out var temperature) || !args.TryInt("max-tokens", out var maxTokens))
                return await Usage("'--temperature' and '--max-tokens' are numbers");

            return await ReplyAsync(Prompts.SavePromptAsync(new PromptDraft
            {
                UseCase = args.Value("use-case")!,
                ModelMatch = args.Value("model-match"),
                SystemTemplate = await ReadTextAsync(args.Value("system-file")!, ct),
                UserTemplate = await ReadTextAsync(args.Value("user-file")!, ct),
                Parameters = new PromptParameters { Temperature = temperature, MaxOutputTokens = maxTokens, Effort = args.Value("effort") },
                Note = args.Value("note"),
                By = args.Value("by"),
            }, ct));
        }

        private async Task<int> ListPromptsAsync(CancellationToken ct)
        {
            var useCase = args.Value("use-case")!;
            var prompts = await Prompts.ListPromptsAsync(useCase, ct);
            if (prompts is Result<IReadOnlyList<PromptModel>>.Failure { Error: var error })
                return await AiOperatorWire.ErrorAsync(output, Command, error);

            var labels = await Prompts.ListLabelsAsync(useCase, ct);
            return await AiOperatorWire.OkAsync(output, Command, new
            {
                Prompts = ((Result<IReadOnlyList<PromptModel>>.Success)prompts).Value,
                Labels = labels is Result<IReadOnlyList<PromptLabelModel>>.Success { Value: var list } ? list : [],
            });
        }

        private async Task<int> MoveLabelAsync(CancellationToken ct) =>
            args.TryInt("version", out var version) && version is { } target
                ? await ReplyAsync(Prompts.MoveLabelAsync(args.Value("use-case")!, args.Value("label")!, target, args.Value("by"), ct))
                : await Usage("'--version' is a number");

        private async Task<int> PutPriceAsync(CancellationToken ct)
        {
            if (!args.TryLong("input", out var input) || !args.TryLong("output", out var outputPrice)
                || !args.TryLong("cache-write", out var cacheWrite) || !args.TryLong("cache-read", out var cacheRead))
                return await Usage("prices are whole micro-USD per 1M tokens");
            if (!args.TryInstant("effective-from", out var from))
                return await Usage("'--effective-from' is a date or an instant");

            return await ReplyAsync(Prices.PutAsync(new ModelPriceModel
            {
                Provider = args.Value("provider")!,
                Model = args.Value("model")!,
                EffectiveFromUtc = from ?? services.GetRequiredService<TimeProvider>().GetUtcNow(),
                InputPerMTokMicroUsd = input!.Value,
                OutputPerMTokMicroUsd = outputPrice!.Value,
                CacheWritePerMTokMicroUsd = cacheWrite,
                CacheReadPerMTokMicroUsd = cacheRead,
                Source = args.Value("source"),
            }, ct));
        }

        private async Task<int> RunUseCaseAsync(CancellationToken ct)
        {
            if (!args.TryInt("version", out var version))
                return await Usage("'--version' is a number");

            var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in args.All("var"))
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals <= 0)
                    return await Usage($"'--var {pair}' is name=value");
                variables[pair[..equals]] = pair[(equals + 1)..];
            }

            foreach (var pair in args.All("var-file"))
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals <= 0)
                    return await Usage($"'--var-file {pair}' is name=path");
                variables[pair[..equals]] = await ReadTextAsync(pair[(equals + 1)..], ct);
            }

            var models = args.List("model");
            var result = await services.GetRequiredService<IAiRunner>().RunAsync(new AiRunRequest
            {
                UseCase = args.Value("use-case")!,
                Variables = variables,
                Subject = args.Value("subject"),
                Version = version,
                Label = args.Value("label"),
                Models = models.Count > 0 ? models : null,
                Fresh = args.Flag("fresh"),
            }, ct);
            if (result is Result<AiRunModel>.Failure { Error: var error })
                return await AiOperatorWire.ErrorAsync(output, Command, error);

            var run = ((Result<AiRunModel>.Success)result).Value;
            return run.Outcome == AiOutcome.Succeeded
                ? await AiOperatorWire.OkAsync(output, Command, run)
                : await AiOperatorWire.ErrorAsync(output, Command, JsonNamingPolicy.KebabCaseLower.ConvertName(run.Outcome.ToString()),
                    run.Final.Error ?? $"The run ended {run.Outcome}.", AiOperatorWire.Refused, run);
        }

        private async Task<int> ListInferencesAsync(CancellationToken ct)
        {
            if (!args.TryGuid("run", out var run) || !args.TryInt("limit", out var limit))
                return await Usage("'--run' is an id and '--limit' a number");

            return await ReplyAsync(Inferences.ListAsync(new InferenceQuery
            {
                UseCase = args.Value("use-case"),
                Subject = args.Value("subject"),
                EvalRunId = run,
                Limit = limit ?? 20,
            }, ct));
        }

        private async Task<int> TotalsAsync(CancellationToken ct) =>
            args.TryInstant("from", out var from) && args.TryInstant("to", out var to)
                ? await ReplyAsync(Inferences.TotalsAsync(from!.Value, to!.Value, ct))
                : await Usage("'--from' and '--to' are dates or instants");

        private Task<int> PutSetAsync(CancellationToken ct) => ReplyAsync(Evals.PutSetAsync(new EvalSetModel
        {
            Key = args.Value("key")!,
            UseCase = args.Value("use-case")!,
            ScoreFields = args.List("score-fields"),
            SevereMisses = args.List("severe"),
            Note = args.Value("note"),
        }, ct));

        private async Task<int> AddItemAsync(CancellationToken ct)
        {
            IReadOnlyDictionary<string, string?>? input = null;
            if (args.Value("input-file") is { } file)
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return await AiOperatorWire.ErrorAsync(output, Command, "invalid", "The input file must hold a JSON object of prompt variables.");
                input = document.RootElement.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => property.Value.GetRawText(),
                    },
                    StringComparer.Ordinal);
            }

            return await ReplyAsync(Evals.AddItemAsync(new EvalItemDraft
            {
                Set = args.Value("set")!,
                Subject = args.Value("subject"),
                Input = input,
                Expected = args.Value("expected")!,
                Tags = args.List("tags"),
                LabelledBy = args.Value("by"),
            }, ct));
        }

        private async Task<int> RelabelAsync(CancellationToken ct) =>
            args.TryGuid("item", out var item)
                ? await ReplyAsync(Evals.RelabelItemAsync(item!.Value, args.Value("expected")!, args.Value("by"), ct))
                : await Usage("'--item' is an id");

        private async Task<int> RemoveAsync(CancellationToken ct)
        {
            if (!args.TryGuid("item", out var item))
                return await Usage("'--item' is an id");

            var removed = await Evals.RemoveItemAsync(item!.Value, ct);
            return removed is Result<bool>.Success { Value: false }
                ? await AiOperatorWire.ErrorAsync(output, Command, "not-found", $"Eval item '{item}' does not exist.")
                : await ReplyAsync(Task.FromResult(removed));
        }

        /// <summary>Runs the matrix: every model × every version, one run each, then prints their summaries.</summary>
        private async Task<int> RunEvalAsync(CancellationToken ct)
        {
            if (!args.TryInt("parallel", out var parallel))
                return await Usage("'--parallel' is a number");

            var versions = new List<int?>();
            foreach (var text in args.List("version"))
            {
                if (!int.TryParse(text, out var version))
                    return await Usage("'--version' is a number or a comma list of numbers");
                versions.Add(version);
            }

            if (versions.Count == 0)
                versions.Add(null);

            var set = args.Value("set")!;
            var runs = new List<Guid>();
            var refused = new List<object>();
            foreach (var model in args.List("model"))
            {
                foreach (var version in versions)
                {
                    var result = await Evals.RunAsync(new EvalRunRequest
                    {
                        Set = set,
                        Model = model,
                        Version = version,
                        Label = args.Value("label"),
                        Fresh = args.Flag("fresh"),
                        Parallelism = parallel ?? 1,
                        Note = args.Value("note"),
                    }, ct);
                    if (result is Result<EvalRunModel>.Success { Value: var run })
                        runs.Add(run.Id);
                    else if (result is Result<EvalRunModel>.Failure { Error: var error })
                        refused.Add(new { Model = model, Version = version, Error = AiOperatorWire.Word(error.Type), error.Message });
                }
            }

            IReadOnlyList<EvalRunSummary> summaries = [];
            if (runs.Count > 0 && await Evals.ReportAsync(set, runs, cancellationToken: ct) is Result<EvalReportModel>.Success { Value: var report })
                summaries = report.Runs;

            var data = new { Runs = summaries, Refused = refused.Count == 0 ? null : refused };
            return refused.Count == 0
                ? await AiOperatorWire.OkAsync(output, Command, data)
                : await AiOperatorWire.ErrorAsync(output, Command, "refused", $"{refused.Count} of {refused.Count + runs.Count} runs could not start.", AiOperatorWire.Refused, data);
        }

        private async Task<int> ReportAsync(CancellationToken ct)
        {
            var format = (args.Value("format") ?? "json").ToLowerInvariant();
            if (format is not ("json" or "csv" or "md"))
                return await Usage("'--format' is json, csv or md");

            var runIds = new List<Guid>();
            foreach (var text in args.List("run"))
            {
                if (!Guid.TryParse(text, out var id))
                    return await Usage("'--run' is an id or a comma list of ids");
                runIds.Add(id);
            }

            var result = await Evals.ReportAsync(args.Value("set")!, runIds, args.Flag("answers"), ct);
            if (result is Result<EvalReportModel>.Failure { Error: var error })
                return await AiOperatorWire.ErrorAsync(output, Command, error);

            var report = ((Result<EvalReportModel>.Success)result).Value;
            if (format == "json")
                return await AiOperatorWire.OkAsync(output, Command, report);

            await output.WriteAsync(format == "md" ? EvalReportFormats.Markdown(report) : EvalReportFormats.Csv(report));
            return AiOperatorWire.Done;
        }

        private async Task<int> RateAsync(CancellationToken ct)
        {
            if (!args.TryGuid("run", out var run) || !args.TryGuid("item", out var item) || !args.TryInt("rating", out var rating))
                return await Usage("'--run' and '--item' are ids, '--rating' is 1 to 5");

            return await ReplyAsync(Evals.RateAsync(new EvalRatingDraft
            {
                RunId = run!.Value,
                ItemId = item!.Value,
                Rating = rating!.Value,
                Comment = args.Value("comment"),
                By = args.Value("by"),
            }, ct));
        }

        private async Task<int> ExportAsync(CancellationToken ct)
        {
            var result = await Evals.ExportSetAsync(args.Value("set")!, ct);
            if (result is Result<EvalSetExport>.Failure { Error: var error })
                return await AiOperatorWire.ErrorAsync(output, Command, error);

            var export = ((Result<EvalSetExport>.Success)result).Value;
            var path = Path.GetFullPath(args.Value("out")!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(export, AiOperatorWire.FileOptions), ct);
            return await AiOperatorWire.OkAsync(output, Command, new { Set = export.Set.Key, Items = export.Items.Count, File = path });
        }

        private async Task<int> ImportAsync(CancellationToken ct)
        {
            var export = JsonSerializer.Deserialize<EvalSetExport>(await File.ReadAllTextAsync(args.Value("file")!, ct), AiOperatorWire.Options);
            if (export is null)
                return await AiOperatorWire.ErrorAsync(output, Command, "invalid", "The file holds no eval set export.");

            var result = await Evals.ImportSetAsync(export, ct);
            if (result is Result<EvalSetModel>.Failure { Error: var error })
                return await AiOperatorWire.ErrorAsync(output, Command, error);

            var items = await Evals.ListItemsAsync(export.Set.Key, ct) is Result<IReadOnlyList<EvalItemModel>>.Success { Value: var list } ? list.Count : 0;
            return await AiOperatorWire.OkAsync(output, Command, new { Set = ((Result<EvalSetModel>.Success)result).Value, Items = items });
        }

        /// <summary>Prints a broker's result: the value, or its error as a refusal.</summary>
        private async Task<int> ReplyAsync<T>(Task<Result<T>> pending) where T : notnull => await pending switch
        {
            Result<T>.Success { Value: var value } => await AiOperatorWire.OkAsync(output, Command, value),
            Result<T>.Failure { Error: var error } => await AiOperatorWire.ErrorAsync(output, Command, error),
            _ => throw new InvalidOperationException("A result is either a success or a failure."),
        };

        private Task<int> Usage(string problem) =>
            AiOperatorWire.ErrorAsync(output, Command, "usage", $"{problem}; usage: {AiArguments.Specs[Command].Usage(Command)}");

        /// <summary>Reads a text file, dropping its trailing line breaks, so an editor's final newline never changes a hash.</summary>
        private static async Task<string> ReadTextAsync(string path, CancellationToken ct) =>
            (await File.ReadAllTextAsync(path, ct)).TrimEnd('\r', '\n');
    }
}
