# wow-two-sdk-beta.ai — Package registry

*Last updated: 2026-11-17*

> Every package this repo publishes, one version line per registry. Layer rules:
> wow-two-ws/conventions/development/repo/structure/domain-structure.md § Layers.

| Package | Layer | References | Heavy deps | Forms | Consumers |
|---|---|---|---|---|---|
| `WoW2.Sdk.Beta.Be.Ai.Abstractions` | contract | the kernel `WoW2.Sdk.Beta.Be.Abstractions` only; no framework reference | none | all | products that call or test the runner |
| `WoW2.Sdk.Beta.Be.Ai.Providers` | edge | Abstractions, core | `Anthropic`, `OpenAI`, `Microsoft.Extensions.AI.OpenAI`; Microsoft.Extensions.AI and OllamaSharp via core | 1 | `…Ai.Module`, products that want a plain `IChatClient` (sift) |
| `WoW2.Sdk.Beta.Be.Ai.Module` | forms | Abstractions, Providers, core | none | 1 | SCFD, Haven, `service/Ai.Api` |
| `WoW2.Sdk.Beta.Be.Ai.Hosting` | hosting | Abstractions, Providers, core, ASP.NET Core | none | 1 (any host), 2, 3 | `service/Ai.Api`; a product host that serves the API itself |
| `WoW2.Sdk.Beta.Be.Ai.Client` | forms | Abstractions, core | none | 2, 3 | products switching to the service (SCFD after the deploy) |
| `WoW2.Sdk.Beta.Be.Ai.Testing` | test kit | Abstractions, Providers, `WoW2.Sdk.Beta.Be.Testing` | xunit (via be Testing) | all | test projects; `PromptsBrokerConformance`, `EvalsBrokerConformance` |
| `@wow-two-beta/fe-ai` | fe | peers `@wow-two-beta/fe` `>=0.0.n <0.1.0`, `vue` | none | all | `apps/web`; product consoles that show eval reports |
