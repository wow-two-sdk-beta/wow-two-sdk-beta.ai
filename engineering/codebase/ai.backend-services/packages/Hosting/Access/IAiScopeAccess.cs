using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

/// <summary>Decides which data scopes a caller of the Ai HTTP API sees and writes: the host's tenant rule.</summary>
/// <remarks>
///   - the API keeps every read, list and write inside the grant: lists filter to it, a run, set or item outside it reads
///     as missing, and a write naming a scope outside it is forbidden
///   - the default (<see cref="UnscopedAiScopeAccess"/>) grants the unscoped bucket only, which fits a single-tenant host;
///     a multi-tenant host registers its own before or after <c>AddAiApi</c>, mapping its principal to
///     <c>tenant:{id}</c> scopes, or <see cref="AiScopeGrant.Every"/> for its operators
///   - use cases, prompts, labels and prices are shared: one prompt serves every scope
/// </remarks>
public interface IAiScopeAccess
{
    /// <summary>Gets the data scopes the request's caller may see and write.</summary>
    /// <param name="context">The request, with its authenticated principal.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    ValueTask<AiScopeGrant> GrantAsync(HttpContext context, CancellationToken cancellationToken);
}

/// <summary>Grants every caller the unscoped bucket only: the default of a single-tenant host.</summary>
public sealed class UnscopedAiScopeAccess : IAiScopeAccess
{
    /// <inheritdoc />
    public ValueTask<AiScopeGrant> GrantAsync(HttpContext context, CancellationToken cancellationToken) => ValueTask.FromResult(AiScopeGrant.Unscoped);
}
