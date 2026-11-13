using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Ai.Client;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Ai.Tests.Conformance.Greetings;

/// <summary>Forms 2 and 3: the client wired as a consumer wires it, against the service host.</summary>
/// <remarks>By default the host runs in process (WebApplicationFactory) over a PostgreSQL container; it boots as a local
/// run, so it applies the module's set. With <c>CONFORMANCE_BASE_URL</c> set, the client calls that running host or image
/// instead, on the real clock, and no container starts.</remarks>
public sealed class ServiceFixture : IAsyncLifetime
{
    /// <summary>Holds the variable that points the suite at a running host or image.</summary>
    public const string BaseUrlVariable = "CONFORMANCE_BASE_URL";

    private readonly Uri? runningHost = Environment.GetEnvironmentVariable(BaseUrlVariable) is { Length: > 0 } url ? new Uri(url) : null;
    private PostgresFixture? postgres;
    private WebApiTestHost<Program>? host;

    /// <summary>Gets the clock the service stamps greetings with.</summary>
    public TimeProvider Clock => host?.Clock ?? TimeProvider.System;

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
                    ["Logging:LogLevel:Default"] = "Warning",
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
