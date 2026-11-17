using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Conformance;

namespace Ai.Tests.Conformance.Client;

/// <summary>Forms 2 and 3: the prompt registry's suite through the HTTP client against the service host.</summary>
public sealed class ClientPromptsConformanceTests(ServiceFixture fixture) : PromptsBrokerConformance, IClassFixture<ServiceFixture>
{
    protected override IPromptsBroker Broker => fixture.Services.GetRequiredService<IPromptsBroker>();
}

/// <summary>Forms 2 and 3: the evals suite through the HTTP client against the service host.</summary>
public sealed class ClientEvalsConformanceTests(ServiceFixture fixture) : EvalsBrokerConformance, IClassFixture<ServiceFixture>
{
    protected override IEvalsBroker Broker => fixture.Services.GetRequiredService<IEvalsBroker>();

    protected override IPromptsBroker Prompts => fixture.Services.GetRequiredService<IPromptsBroker>();
}
