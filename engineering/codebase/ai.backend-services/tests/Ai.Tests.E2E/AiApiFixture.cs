using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;
using WoW.Two.Sdk.Beta.Be.Ai.Module;
using WoW.Two.Sdk.Beta.Be.Ai.Providers;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Providers;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using WoW.Two.Sdk.Beta.Be.Testing.MultiHost;

namespace Ai.Tests.E2E;

/// <summary>Boots the real API over an isolated PostgreSQL container; the test host is a local run, so its boot applies the
/// SQL migrations, and its eval worker drains the queue.</summary>
/// <remarks>
///   - tokens: the host's token check trusts a test issuer's key (<see cref="Token"/>), so routes see real JWTs with
///     <c>scope</c> claims
///   - data scopes: a token's <c>tenant</c> claim grants <c>tenant:{id}</c>, an <c>operator</c> claim every scope, and
///     none the unscoped bucket
///   - the providers are scripted (<see cref="Caller"/>): every call answers <c>{"relevance":"close"}</c> unless a test
///     sets its own answer
///   - a public image: the host serves <see cref="Poster"/> at <see cref="PosterPath"/>, and the module's image client
///     reaches the host through the test server, as it would a CDN over the network
/// </remarks>
public sealed class AiApiFixture : MultiHostFixture, IAsyncLifetime
{
    /// <summary>Holds the public host the test target allows, beside <c>localhost</c>, as a deployed target's <c>AllowedHosts</c> does.</summary>
    public const string PublicHost = "ai.example.test";

    /// <summary>Holds the test issuer.</summary>
    public const string Issuer = "https://identity.example.test/";

    /// <summary>Holds this service's audience.</summary>
    public const string Audience = "https://ai.example.test/";

    /// <summary>Holds the path the test host serves <see cref="Poster"/> at, a public image.</summary>
    public const string PosterPath = "/e2e/poster.png";

    /// <summary>Gets the poster's bytes: a PNG signature and a few bytes, enough to read as a PNG.</summary>
    public static byte[] Poster { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("ai-e2e-signing-key-0123456789abcdef-not-a-secret"));

    private readonly PostgresFixture postgres;

    public AiApiFixture()
    {
        postgres = AddSharedFixture(new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build()));
        Host = AddHost(new WebApiTestHost<Program>
        {
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
                ["AllowedHosts"] = PublicHost + ";localhost",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Ai:Module:RetryDelay"] = "00:00:00",
                ["Ai:Module:ImageFetchHosts:0"] = "localhost",
                ["Ai:Worker:PollInterval"] = "00:00:00.100",
                // The host runs as Development, which loads the developer's user-secrets; blank keys keep the tests
                // independent of a key set on the machine for the local eval bench.
                ["Ai:Providers:anthropic:ApiKey"] = "",
                ["Ai:Providers:openai:ApiKey"] = "",
            }),
            ConfigureServicesHook = services =>
            {
                services.AddSingleton<IStartupFilter, PosterFilter>();
                services.AddHttpClient(AiModuleConstants.ImageHttpClientName).ConfigurePrimaryHttpMessageHandler(() => Host!.Server.CreateHandler());
                services.AddSingleton<IAiModelCaller>(Caller);
                services.AddSingleton<IAiScopeAccess, ClaimScopeAccess>();
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = null;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = Issuer,
                        ValidAudience = Audience,
                        IssuerSigningKey = SigningKey,
                    };
                });
            },
        });
    }

    /// <summary>Gets the booted API host.</summary>
    public WebApiTestHost<Program> Host { get; }

    /// <summary>Gets the scripted providers every host call goes to.</summary>
    public ScriptedAiModelCaller Caller { get; } = new()
    {
        Respond = _ => ScriptedAiModelCaller.Answer("""{"relevance":"close"}""", inputTokens: 1000, outputTokens: 100),
    };

    /// <summary>Mints a token from the test issuer for this service, with the scopes and data-scope claims given; it holds
    /// on any clock, the host's fake one included.</summary>
    /// <param name="scopes">The <c>ai.*</c> scopes, space-separated.</param>
    /// <param name="tenant">The tenant id the caller acts for, when any.</param>
    /// <param name="isOperator">Whether the caller sees every scope.</param>
    /// <param name="audience">The audience; another service's is refused.</param>
    public static string Token(string scopes, string? tenant = null, bool isOperator = false, string audience = Audience)
    {
        var claims = new Dictionary<string, object> { ["sub"] = "e2e-client", ["scope"] = scopes };
        if (tenant is not null)
            claims["tenant"] = tenant;
        if (isOperator)
            claims["operator"] = "true";
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IssuedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Expires = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });
    }

    Task IAsyncLifetime.InitializeAsync() => StartAsync().AsTask();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    /// <summary>Serves <see cref="Poster"/> at <see cref="PosterPath"/> ahead of the host's own pipeline, as a public image host.</summary>
    private sealed class PosterFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                if (context.Request.Path != PosterPath)
                {
                    await following(context);
                    return;
                }

                context.Response.ContentType = "image/png";
                await context.Response.Body.WriteAsync(Poster);
            });
            next(app);
        };
    }

    /// <summary>Grants a token's <c>tenant</c> claim as <c>tenant:{id}</c>, an <c>operator</c> claim every scope, and none
    /// the unscoped bucket: a multi-tenant host's rule.</summary>
    private sealed class ClaimScopeAccess : IAiScopeAccess
    {
        public ValueTask<AiScopeGrant> GrantAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var user = context.User;
            if (user.FindFirstValue("operator") == "true")
                return ValueTask.FromResult(AiScopeGrant.Every);
            return ValueTask.FromResult(user.FindFirstValue("tenant") is { } tenant ? AiScopeGrant.Of("tenant:" + tenant) : AiScopeGrant.Unscoped);
        }
    }
}

/// <summary>Shares one <see cref="AiApiFixture"/> across the whole E2E run.</summary>
[CollectionDefinition(Name)]
public sealed class AiApiCollection : ICollectionFixture<AiApiFixture>
{
    /// <summary>The collection name every E2E class joins.</summary>
    public const string Name = "ai-api";
}
