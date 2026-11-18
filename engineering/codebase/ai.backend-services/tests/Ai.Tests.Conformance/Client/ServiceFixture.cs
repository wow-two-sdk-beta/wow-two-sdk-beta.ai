using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Ai.Client;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Ai.Tests.Conformance.Client;

/// <summary>Forms 2 and 3: the client wired as a consumer wires it (<c>AddAiClient</c>), against the service host.</summary>
/// <remarks>By default the host runs in process (WebApplicationFactory) over a PostgreSQL container, as a local run that
/// applies the module's set, with its scopes lifted for Development and its worker off, so a queued run waits. With
/// <c>CONFORMANCE_BASE_URL</c> set, the client calls that running host or image instead, which must run the same way
/// (<c>Ai__Api__AllowAnonymousInDevelopment=true</c>, <c>Ai__Worker__Enabled=false</c>), and no container starts.</remarks>
public sealed class ServiceFixture : IAsyncLifetime
{
    /// <summary>Holds the variable that points the suite at a running host or image.</summary>
    public const string BaseUrlVariable = "CONFORMANCE_BASE_URL";

    private readonly Uri? runningHost = Environment.GetEnvironmentVariable(BaseUrlVariable) is { Length: > 0 } url ? new Uri(url) : null;
    private PostgresFixture? postgres;
    private WebApiTestHost<Program>? host;

    /// <summary>Gets the consumer's container.</summary>
    public ServiceProvider Services { get; private set; } = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        var services = new ServiceCollection().AddLogging();
        if (runningHost is not null)
        {
            services.AddAiClient(options => options.BaseAddress = runningHost);
        }
        else
        {
            postgres = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
            await postgres.StartAsync();
            var connectionString = postgres.ConnectionString;
            host = new WebApiTestHost<Program>
            {
                ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseSettings:ConnectionString"] = connectionString,
                    ["Ai:Api:AllowAnonymousInDevelopment"] = "true",
                    ["Ai:Worker:Enabled"] = "false",
                    ["Ai:Module:RetryDelay"] = "00:00:00",
                    ["Logging:LogLevel:Default"] = "Warning",
                    // Development loads the developer's user-secrets; blank keys keep the suite off real providers.
                    ["Ai:Providers:anthropic:ApiKey"] = "",
                    ["Ai:Providers:openai:ApiKey"] = "",
                }),
            };
            var server = host.Server;
            services.AddAiClient(options => options.BaseAddress = server.BaseAddress)
                .ConfigurePrimaryHttpMessageHandler(server.CreateHandler);
        }

        Services = services.BuildServiceProvider(validateScopes: true);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.DisposeAsync();
        if (host is null)
            return;

        await host.DisposeAsync();
        await postgres!.DisposeAsync();
    }
}
