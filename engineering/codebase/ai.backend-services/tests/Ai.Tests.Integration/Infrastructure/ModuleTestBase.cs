using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Data.Migrations;

namespace Ai.Tests.Integration.Infrastructure;

/// <summary>The module wired as a consumer wires it over a fresh database per test, its own set applied, with the
/// scripted model caller in place of the providers and a fake clock after the seeded prices take effect.</summary>
/// <param name="fixture">A container for the class, reset before each test.</param>
public abstract class ModuleTestBase(MigratorPostgresFixture fixture) : IClassFixture<MigratorPostgresFixture>, IAsyncLifetime
{
    /// <summary>The route most tests use: Haiku, then Luna.</summary>
    protected const string Haiku = "anthropic/claude-haiku-5-5";

    /// <summary>The fallback model.</summary>
    protected const string Luna = "openai/gpt-6-luna";

    /// <summary>The relevance answer's schema.</summary>
    protected const string RelevanceSchema = """
        {"type":"object","required":["relevance","kind"],
         "properties":{"relevance":{"type":"string","enum":["close","far","none"]},"kind":{"type":"string"}}}
        """;

    private ServiceProvider services = null!;

    /// <summary>Gets the clock: 2026-11-12 10:00 UTC, after the seeded prices.</summary>
    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    /// <summary>Gets the scripted providers.</summary>
    protected ScriptedAiModelCaller Caller { get; private set; } = null!;

    /// <summary>Gets the container's connection string.</summary>
    protected string ConnectionString => fixture.ConnectionString;

    /// <summary>Gets the consumer's container.</summary>
    protected IServiceProvider Services => services;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await BuildAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await services.DisposeAsync();

    /// <summary>Changes the module's settings before the container is built; the default skips retry waits.</summary>
    /// <param name="options">The settings.</param>
    protected virtual void Configure(AiModuleOptions options) => options.RetryDelay = TimeSpan.Zero;

    /// <summary>Adds services before the container is built.</summary>
    /// <param name="collection">The services.</param>
    protected virtual void AddServices(IServiceCollection collection)
    {
    }

    /// <summary>Drops every schema and builds the container again, as a fresh database for an import.</summary>
    protected async Task ResetDatabaseAsync()
    {
        await services.DisposeAsync();
        await fixture.ResetAsync();
        await BuildAsync();
    }

    /// <summary>Runs <paramref name="work"/> in a scope of its own, as one request would.</summary>
    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    /// <summary>Runs a use case.</summary>
    protected Task<Result<AiRunModel>> RunAsync(AiRunRequest request) =>
        InScopeAsync(provider => provider.GetRequiredService<IAiRunner>().RunAsync(request));

    /// <summary>Calls the prompt registry.</summary>
    protected Task<T> PromptsAsync<T>(Func<IPromptsBroker, Task<T>> call) => InScopeAsync(provider => call(provider.GetRequiredService<IPromptsBroker>()));

    /// <summary>Calls evals.</summary>
    protected Task<T> EvalsAsync<T>(Func<IEvalsBroker, Task<T>> call) => InScopeAsync(provider => call(provider.GetRequiredService<IEvalsBroker>()));

    /// <summary>Calls the inference log.</summary>
    protected Task<T> InferencesAsync<T>(Func<IInferencesBroker, Task<T>> call) => InScopeAsync(provider => call(provider.GetRequiredService<IInferencesBroker>()));

    /// <summary>Calls prices.</summary>
    protected Task<T> PricesAsync<T>(Func<IModelPricesBroker, Task<T>> call) => InScopeAsync(provider => call(provider.GetRequiredService<IModelPricesBroker>()));

    /// <summary>Puts a use case with one generic prompt that renders <c>{{title}}</c>.</summary>
    protected async Task<string> UseCaseAsync(string key = "relevance", PromptMode mode = PromptMode.Rewrite, string? schema = RelevanceSchema,
        IReadOnlyList<string>? route = null, bool keepOutputs = true, string user = "Post: {{title}}")
    {
        (await PromptsAsync(prompts => prompts.PutUseCaseAsync(new UseCaseModel
        {
            Key = key,
            PromptMode = mode,
            OutputSchema = schema,
            Route = route ?? [Haiku, Luna],
            KeepOutputs = keepOutputs,
        }))).ValueOrThrow();
        await SaveAsync(key, user);
        return key;
    }

    /// <summary>Saves a prompt.</summary>
    protected async Task<PromptModel> SaveAsync(string useCase, string user, string? match = null, PromptParameters? parameters = null) =>
        (await PromptsAsync(prompts => prompts.SavePromptAsync(new PromptDraft
        {
            UseCase = useCase,
            ModelMatch = match,
            SystemTemplate = "You rate a post's relevance to fashion events. Answer JSON.",
            UserTemplate = user,
            Parameters = parameters ?? new PromptParameters(),
        }))).ValueOrThrow();

    /// <summary>Builds the variables of a post.</summary>
    protected static Dictionary<string, string?> Post(string title) => new() { ["title"] = title };

    /// <summary>Builds a JSON answer.</summary>
    protected static string Json(string relevance, string kind = "runway") => $$"""{"relevance":"{{relevance}}","kind":"{{kind}}"}""";

    private async Task BuildAsync()
    {
        Caller = new ScriptedAiModelCaller(Clock);
        var collection = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(Clock).AddSingleton<IAiModelCaller>(Caller);
        collection.AddAi().Configure(options =>
        {
            options.ConnectionString = fixture.ConnectionString;
            Configure(options);
        });
        AddServices(collection);
        services = collection.BuildServiceProvider(validateScopes: true);

        // The consumer's deploy step: every registered set, the module's included.
        (await services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }
}
