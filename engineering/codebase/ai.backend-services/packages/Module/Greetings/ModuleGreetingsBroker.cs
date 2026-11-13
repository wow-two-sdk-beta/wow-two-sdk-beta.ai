using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Ai.Greetings;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Greetings;

/// <summary>Integrates the in-process greetings module: composes the greeting in the consumer's process.</summary>
internal sealed class ModuleGreetingsBroker(TimeProvider time) : IGreetingsBroker
{
    /// <inheritdoc />
    public Task<Result<GreetingModel>> GreetAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (name is { Length: > GreetingConstants.MaxNameLength })
            return Task.FromResult(Result<GreetingModel>.Fail(GreetingErrors.NameTooLong()));

        return Task.FromResult(Result<GreetingModel>.Ok(GreetingComposer.Compose(name, time.GetUtcNow())));
    }
}
