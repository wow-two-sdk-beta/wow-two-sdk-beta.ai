using Ai.Api.Operator;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace Ai.Api.Configurations;

/// <summary>Registers the service's own one-off verbs with the be sdk's <see cref="ServiceHost"/>, beside its <c>migrate</c>
/// and <c>config check</c>.</summary>
/// <remarks>The <c>ai</c> family runs before any host is built, reading the configuration itself and opening the module
/// over the database; none starts a listener or a hosted service.</remarks>
public static class AiVerbs
{
    /// <summary>Adds the <c>ai</c> verb family (<see cref="AiCommand"/>).</summary>
    /// <param name="verbs">The service's verbs.</param>
    /// <returns>The verbs, for chaining.</returns>
    public static ServiceVerbs AddAiVerbs(this ServiceVerbs verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        return verbs.AddPreHostVerb(AiCommand.Verb, (verb, cancellationToken) => AiCommand.RunAsync(verb.Arguments, verb.Output, cancellationToken));
    }
}
