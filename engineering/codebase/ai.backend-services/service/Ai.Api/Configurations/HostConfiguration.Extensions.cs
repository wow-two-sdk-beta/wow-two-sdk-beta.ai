using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Web.Json;

namespace Ai.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Hosts the Form 1 module over the service's PostgreSQL database; the module registers its own migration set.</summary>
    private static WebApplicationBuilder AddAiModule(this WebApplicationBuilder builder)
    {
        // Resolved on first use, so a test host's configuration overrides still reach the connection string.
        builder.Services.AddAi(AiModuleConstants.ServiceSchemaOwner)
            .Configure<IConfiguration>((options, configuration) => options.ConnectionString = ResolveConnectionString(configuration));
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
