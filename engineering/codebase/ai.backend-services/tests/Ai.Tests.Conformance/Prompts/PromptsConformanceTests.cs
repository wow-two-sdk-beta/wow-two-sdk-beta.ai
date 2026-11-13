using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Testing.Conformance;

namespace Ai.Tests.Conformance.Prompts;

/// <summary>Form 1: the prompt registry's suite against the in-process module, one scope per test.</summary>
public sealed class ModulePromptsConformanceTests(ModuleFixture fixture) : PromptsBrokerConformance, IClassFixture<ModuleFixture>, IDisposable
{
    private readonly AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

    protected override IPromptsBroker Broker => scope.ServiceProvider.GetRequiredService<IPromptsBroker>();

    public void Dispose() => scope.Dispose();
}
