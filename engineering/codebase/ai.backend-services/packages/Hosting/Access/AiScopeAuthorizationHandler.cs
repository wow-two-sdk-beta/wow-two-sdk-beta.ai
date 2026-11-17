using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

/// <summary>Grants an <see cref="AiScopeRequirement"/> when the authenticated principal lists its scope in a scope claim:
/// <c>scope</c> (OAuth 2.0, space-separated), <c>scp</c> (Entra) or the mapped Microsoft claim type, as one value or
/// several.</summary>
public sealed class AiScopeAuthorizationHandler : AuthorizationHandler<AiScopeRequirement>
{
    /// <summary>Holds the claim types a scope is read from.</summary>
    public static IReadOnlyList<string> ScopeClaimTypes { get; } = ["scope", "scp", "http://schemas.microsoft.com/identity/claims/scope"];

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AiScopeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        if (context.User.Identity?.IsAuthenticated == true && HasScope(context.User, requirement.Scope))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    /// <summary>Gets whether a principal lists a scope.</summary>
    /// <param name="user">The principal.</param>
    /// <param name="scope">The scope.</param>
    public static bool HasScope(ClaimsPrincipal user, string scope)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Claims
            .Where(claim => ScopeClaimTypes.Contains(claim.Type, StringComparer.Ordinal))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(scope, StringComparer.Ordinal);
    }
}
