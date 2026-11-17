using Microsoft.AspNetCore.Authentication.JwtBearer;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting;
using WoW.Two.Sdk.Beta.Be.Ai.Module;

namespace Ai.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>The configuration section of the token check: <c>Authority</c> (Identity's issuer) and <c>Audience</c>
    /// (this service's resource URI).</summary>
    public const string AuthSectionName = "Ai:Auth";

    /// <summary>The configuration section of the eval worker (<see cref="AiEvalWorkerOptions"/>): <c>Enabled</c> (default
    /// true), <c>PollInterval</c>, <c>OrphanAfter</c>, <c>MaxConcurrentRuns</c>.</summary>
    public const string WorkerSectionName = "Ai:Worker";

    /// <summary>Hosts the HTTP API with its scope policies (<c>Ai:Api</c>) and Identity's token check: a bearer token is
    /// verified offline against the issuer's keys, its audience and lifetime, and its <c>scope</c> claim names the routes
    /// it reaches. With no authority configured every token fails, so only a Development host that sets
    /// <c>Ai:Api:AllowAnonymousInDevelopment</c> answers without one.</summary>
    private static WebApplicationBuilder AddAiAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddAiApi().BindConfiguration(AiApiOptions.SectionName);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((options, configuration) =>
        {
            var section = configuration.GetSection(AuthSectionName);
            options.MapInboundClaims = false;
            options.Authority = string.IsNullOrWhiteSpace(section["Authority"]) ? null : section["Authority"];
            options.Audience = string.IsNullOrWhiteSpace(section["Audience"]) ? null : section["Audience"];
            options.RequireHttpsMetadata = !isDevelopment;
            options.TokenValidationParameters.NameClaimType = "sub";
        });
        return builder;
    }

    /// <summary>Runs queued evals in this service unless <c>Ai:Worker:Enabled</c> is false, as on a second instance that
    /// only serves the API; the settings are read when the host starts, so a test host's overrides reach them.</summary>
    private static WebApplicationBuilder AddAiWorker(this WebApplicationBuilder builder)
    {
        builder.Services.AddAiEvalWorker().BindConfiguration(WorkerSectionName);
        return builder;
    }
}
