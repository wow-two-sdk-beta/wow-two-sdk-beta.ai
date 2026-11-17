using Ai.Tests.Conformance.Prompts;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Conformance;

namespace Ai.Tests.Conformance.Evals;

/// <summary>Form 1: the evals suite against the in-process module, one scope per test; no worker runs.</summary>
public sealed class ModuleEvalsConformanceTests(ModuleFixture fixture) : EvalsBrokerConformance, IClassFixture<ModuleFixture>, IDisposable
{
    private readonly AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

    protected override IEvalsBroker Broker => scope.ServiceProvider.GetRequiredService<IEvalsBroker>();

    protected override IPromptsBroker Prompts => scope.ServiceProvider.GetRequiredService<IPromptsBroker>();

    public void Dispose() => scope.Dispose();
}
