# wow-two-sdk-beta.ai — Architecture

*Last updated: 2026-11-13*

One contract, a provider edge, a module that owns the state, and one service that hosts the module and its verbs.

```
product code ──► Abstractions (IAiRunner, IPromptsBroker, IEvalsBroker, …)
product composition root ──► AddAiProviders(configuration) + AddAi()            (Form 1)
Module ──► Providers (IAiModelCaller) ──► Anthropic · OpenAI-compatible · Ollama
Module ──► the consumer's PostgreSQL, schema ai: use cases, prompts, labels, prices, inferences, eval sets, runs, scores
service/Ai.Api ──► Module + Providers: `ai …` operator verbs (the local eval bench), status, the console
```

| Form | Registration | Store owner |
|---|---|---|
| 1 · library | `services.AddAiProviders(configuration)` + `services.AddAi()` + the module's migrations | the consumer |
| 2 · self-hosted | the `ai` image; its verbs serve the local eval bench; HTTP brokers later | the container |
| 3 · provided | backlog | the central instance |

| Project | Responsibility |
|---|---|
| `packages/Abstractions` | the contract: runner, prompts, prices, inferences, evals; models, constants, errors; no DI, options or I/O |
| `packages/Providers` | named `IChatClient`s for Anthropic, OpenAI-compatible hosts and Ollama from `Ai:Providers:{name}`; `IAiModelCaller`: one attempt with usage, latency and a classified error |
| `packages/Module` | Form 1: the brokers, the runner (route, retries, fallback, schema check, cache, cost, OpenTelemetry tags), evals and the report view, embedded SQL migrations with the seeded prices |
| `packages/Client` | Forms 2/3: `AddAiClient` and its named client; brokers land with the HTTP API |
| `packages/Testing` | `ScriptedAiModelCaller` and `PromptsBrokerConformance` |
| `service/Ai.Api` | hosting, the `ai` operator verbs, the system status, the console |
| `fe-ai` | the TypeScript contract mirror, formatters (`./kit`), `EvalReportView` (`./admin`) |
| `apps/web` | the console (`/`): system status |
