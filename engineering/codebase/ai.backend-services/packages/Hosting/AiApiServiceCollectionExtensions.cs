using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting;

/// <summary>Registers the Ai HTTP API's services.</summary>
public static class AiApiServiceCollectionExtensions
{
    /// <summary>Registers the API's access: one authorization policy per <see cref="AiScopeConstants"/> scope
    /// (<see cref="AiApiPolicies"/>), the handler that reads scope claims, the default data-scope seam
    /// (<see cref="UnscopedAiScopeAccess"/>, kept when the host registered its own) and <see cref="AiApiOptions"/>.</summary>
    /// <remarks>
    ///   - the brokers come from the host: <c>AddAi()</c> with <c>AddAiProviders(configuration)</c> in process, or any other
    ///     implementation of the contract
    ///   - authentication comes from the host too; with no token a route answers 401, with a token lacking the route's scope
    ///     403
    ///   - <see cref="AiApiOptions.AllowAnonymousInDevelopment"/> lifts the scopes in Development only; anywhere else the
    ///     host fails to start
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Changes the API's settings.</param>
    /// <returns>The options builder, to bind the settings from configuration (<see cref="AiApiOptions.SectionName"/>).</returns>
    public static OptionsBuilder<AiApiOptions> AddAiApi(this IServiceCollection services, Action<AiApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAiScopeAccess, UnscopedAiScopeAccess>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, AiScopeAuthorizationHandler>());
        services.AddAuthorization();
        services.AddOptions<AuthorizationOptions>().Configure<IOptions<AiApiOptions>, IHostEnvironment>((authorization, api, environment) =>
        {
            var anonymous = api.Value.AllowAnonymousInDevelopment && environment.IsDevelopment();
            foreach (var scope in AiScopeConstants.All)
            {
                authorization.AddPolicy(AiApiPolicies.For(scope), policy =>
                {
                    if (anonymous)
                        policy.RequireAssertion(_ => true);
                    else
                        policy.RequireAuthenticatedUser().AddRequirements(new AiScopeRequirement(scope));
                });
            }
        });

        var options = services.AddOptions<AiApiOptions>();
        if (configure is not null)
            options.Configure(configure);
        options.Validate<IHostEnvironment>((settings, environment) => !settings.AllowAnonymousInDevelopment || environment.IsDevelopment(),
                $"{AiApiOptions.SectionName}:{nameof(AiApiOptions.AllowAnonymousInDevelopment)} lifts the ai.* scopes in the Development environment only.")
            .ValidateOnStart();
        return options;
    }
}
