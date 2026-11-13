using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Runs;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module;

/// <summary>Registers the in-process Ai module (Form 1).</summary>
public static class AiModuleServiceCollectionExtensions
{
    /// <summary>Registers the runner (<see cref="IAiRunner"/>), the prompt registry (<see cref="IPromptsBroker"/>), model prices
    /// (<see cref="IModelPricesBroker"/>), the inference log (<see cref="IInferencesBroker"/>) and evals
    /// (<see cref="IEvalsBroker"/>), the module's store on its own keyed Npgsql data source, its migration set
    /// (<see cref="AiModuleConstants.MigrationSetName"/>, in schema <see cref="AiModuleConstants.SchemaName"/>) and the
    /// store's readiness check.</summary>
    /// <remarks>
    ///   - it registers no provider: the host composes <c>AddAiProviders(configuration)</c> from the Providers package
    ///     beside it, which supplies the runner's <c>IAiModelCaller</c>
    ///   - a product adds an <see cref="IEvalSubjectSource"/> per subject prefix to let eval items be added from subjects
    ///   - nothing it registers is unkeyed infrastructure, so it sits beside a host's own database and default set
    ///   - the host's migrate verbs and its boot helper (<c>MigrateBespokeOnStartupAsync</c>, which applies on a local run
    ///     only) reach the set with every other registered set
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="schemaOwner">The kind of host claiming the schema in the migrator's owner marker:
    /// <see cref="AiModuleConstants.ModuleSchemaOwner"/> for a Form 1 product, <see cref="AiModuleConstants.ServiceSchemaOwner"/>
    /// for the Ai service. A schema claimed by one kind refuses the other until an audited <c>claim</c> moves it.</param>
    /// <returns>The options builder, through which the host sets the connection string and the runner's settings; they
    /// are read on first use.</returns>
    public static OptionsBuilder<AiModuleOptions> AddAi(this IServiceCollection services, string schemaOwner = AiModuleConstants.ModuleSchemaOwner)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaOwner);

        services.TryAddSingleton(TimeProvider.System);

        // The set's own database, so the be sdk's boot and deploy helpers create it when missing.
        services.AddKeyedSingleton(AiModuleConstants.ServiceKey, (provider, _) =>
            new DatabaseSettings { ConnectionString = ConnectionString(provider) });

        // One pool for the context and the migration set, disposed with the container.
        services.AddKeyedSingleton(AiModuleConstants.ServiceKey, (provider, _) => NpgsqlDataSource.Create(ConnectionString(provider)));
        services.AddKeyedSingleton<IDbConnectionFactory>(AiModuleConstants.ServiceKey, (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredKeyedService<NpgsqlDataSource>(AiModuleConstants.ServiceKey)));
        services.AddDbContext<AiDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredKeyedService<NpgsqlDataSource>(AiModuleConstants.ServiceKey))
            .UseSnakeCaseNamingConvention());

        // The schema is the module's: its embedded SQL, under its own history, lock and owner marker, in its own schema.
        services.AddDatabaseBespokeMigrationSet(AiModuleConstants.MigrationSetName, typeof(AiModuleAssembly).Assembly, migrations =>
        {
            migrations.Provider = DatabaseProvider.Postgres;
            migrations.SchemaName = AiModuleConstants.SchemaName;
            migrations.SchemaOwner = schemaOwner;
        });

        services.AddScoped<IPromptsBroker, ModulePromptsBroker>();
        services.AddScoped<IModelPricesBroker, ModuleModelPricesBroker>();
        services.AddScoped<IInferencesBroker, ModuleInferencesBroker>();
        services.AddScoped<IAiRunner, ModuleAiRunner>();
        services.AddScoped<IEvalsBroker, ModuleEvalsBroker>();
        services.AddHealthChecks().AddDatabaseReadinessCheck<AiDbContext>();

        return services.AddOptions<AiModuleOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "AiModuleOptions.ConnectionString must be set.")
            .Validate(options => options.RetriesPerModel is >= 0 and <= 10, "AiModuleOptions.RetriesPerModel must be 0 to 10.")
            .Validate(options => options.RetryDelay >= TimeSpan.Zero, "AiModuleOptions.RetryDelay must not be negative.")
            .Validate(options => options.DefaultMaxOutputTokens > 0, "AiModuleOptions.DefaultMaxOutputTokens must be 1 or more.")
            .Validate(options => options.MaxEvalParallelism is >= 1 and <= 16, "AiModuleOptions.MaxEvalParallelism must be 1 to 16.");
    }

    private static string ConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<AiModuleOptions>>().Value.ConnectionString;
}
