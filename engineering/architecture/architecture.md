# wow-two-sdk-beta.ai — Architecture

*Last updated: 2026-11-17*

One contract, a provider edge, a module that owns the state, an HTTP API any host mounts, a client over it, and one
service that hosts the module, the API, the worker and the verbs.

```
product code ──► Abstractions (IAiRunner, IPromptsBroker, IEvalsBroker, …)
product composition root ──► AddAiProviders(configuration) + AddAi() [+ AddAiEvalWorker()]   (Form 1)
                         └─► AddAiClient(…)  — the same brokers over HTTP                    (Forms 2/3)
Module ──► Providers (IAiModelCaller, IAiProviderCatalog) ──► Anthropic · OpenAI-compatible · Ollama
Module ──► the consumer's PostgreSQL, schema ai: use cases, prompts, labels, prices, inferences, eval sets, batches,
           runs, scores — each inference, set, batch and run with its data scope
Hosting ──► AddAiApi() + MapAiApi(): routes under api/ai/ over the registered brokers, ai.* scope policies, IAiScopeAccess
Client ──► Hosting's routes: ApiAiRunner, ApiPromptsBroker, ApiModelPricesBroker, ApiInferencesBroker, ApiEvalsBroker
service/Ai.Api ──► Module + Providers + Hosting: the API with Identity's token check, the eval worker, `ai …` operator
                   verbs (the local eval bench), status, the console
```

- Switching a product from Form 1 to the service is a registration swap: `AddAiClient` in place of `AddAi()` and
  `AddAiProviders`; the code that calls the brokers stays. A data scope (`tenant:{id}`) set on calls and sets in process
  keeps the same meaning over HTTP, where the service's `IAiScopeAccess` grants it per caller.
- Queued evals repeat: a batch pins its sample, expected answers and scoring rules, each run its resolved prompt; the
  worker (in the service, or any host that adds it) claims with `FOR UPDATE SKIP LOCKED`, beats, stops between items on a
  cancel, and fails orphans.

| Form | Registration | Store owner |
|---|---|---|
| 1 · library | `services.AddAiProviders(configuration)` + `services.AddAi()` + the module's migrations | the consumer |
| 2 · self-hosted | the `ai` image (API, worker, verbs, console) + `services.AddAiClient(…)` in the product | the container |
| 3 · provided | the same image run centrally, its tenants kept apart by data scopes (OQ-2) | the central instance |

| Project | Responsibility |
|---|---|
| `packages/Abstractions` | the contract: runner, prompts, prices, inferences, evals; models, constants, errors; no DI, options or I/O |
| `packages/Providers` | named `IChatClient`s for Anthropic, OpenAI-compatible hosts and Ollama from `Ai:Providers:{name}`; `IAiModelCaller`: one attempt with usage, latency and a classified error |
| `packages/Module` | Form 1: the brokers, the runner (route, retries, fallback, schema check, scoped cache, cost, OpenTelemetry tags), evals with pinned batches, the queue and the opt-in worker, the report view, embedded SQL migrations with the seeded prices |
| `packages/Hosting` | the HTTP API: `AddAiApi` (scope policies, `IAiScopeAccess`), `MapAiApi` (routes under `api/ai/`), report formats, the provider overview |
| `packages/Client` | Forms 2/3: `AddAiClient` and the five HTTP brokers over one named client |
| `packages/Testing` | `ScriptedAiModelCaller`, `PromptsBrokerConformance` and `EvalsBrokerConformance` |
| `service/Ai.Api` | hosting, the API with Identity's token check (`Ai:Auth`), the eval worker (`Ai:Worker`), the `ai` operator verbs, the system status, the console |
| `fe-ai` | the TypeScript contract mirror (models, routes, scopes), formatters (`./kit`), `EvalReportView` (`./admin`) |
| `apps/web` | the console (`/`): system status |
