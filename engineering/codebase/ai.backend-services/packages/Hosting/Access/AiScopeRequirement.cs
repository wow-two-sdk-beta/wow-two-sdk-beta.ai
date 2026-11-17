using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

/// <summary>Requires an <c>ai.*</c> scope on the caller's token. <see cref="AiScopeAuthorizationHandler"/> reads it from the
/// principal's scope claims; a host whose principal carries permissions another way adds its own handler for this
/// requirement.</summary>
/// <param name="Scope">The scope, one of <see cref="AiScopeConstants.All"/>.</param>
public sealed record AiScopeRequirement(string Scope) : IAuthorizationRequirement;
