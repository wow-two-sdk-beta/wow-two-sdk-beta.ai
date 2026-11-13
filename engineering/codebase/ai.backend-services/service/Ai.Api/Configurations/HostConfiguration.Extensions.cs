using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Web.Json;

namespace Ai.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>The configuration section the module's runner settings bind from (<see cref="AiModuleOptions"/>).</summary>
    public const string ModuleSectionName = "Ai:Module";

    /// <summary>Fills the module's settings: the runner's from <see cref="ModuleSectionName"/>, then the connection string.</summary>
    /// <param name="options">The settings to fill.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connectionString">The database's connection string.</param>
    public static void BindModule(AiModuleOptions options, IConfiguration configuration, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.GetSection(ModuleSectionName).Bind(options);
        options.ConnectionString = connectionString;
    }

    /// <summary>Hosts the Form 1 module over the service's PostgreSQL database, with the named model providers
    /// (<c>Ai:Providers:{name}</c>) it calls; the module registers its own migration set.</summary>
    private static WebApplicationBuilder AddAiModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddAiProviders(builder.Configuration);

        // Resolved on first use, so a test host's configuration overrides still reach the connection string.
        builder.Services.AddAi(AiModuleConstants.ServiceSchemaOwner)
            .Configure<IConfiguration>((options, configuration) => BindModule(options, configuration, ResolveConnectionString(configuration)));
        return builder;
    }

    /// <summary>Registers controllers on the SDK wire contract.</summary>
    private static WebApplicationBuilder AddControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Resolves the connection string: the <c>DB_CONNECTION</c> alias wins over <c>DatabaseSettings:ConnectionString</c>.</summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration["DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = configuration["DatabaseSettings:ConnectionString"];

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("Database connection string not found. Set DB_CONNECTION or DatabaseSettings:ConnectionString.")
            : connectionString;
    }
}
