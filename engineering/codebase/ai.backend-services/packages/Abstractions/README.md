# WoW2.Sdk.Beta.Be.Ai.Abstractions

The Ai contract: the runner (`IAiRunner`, `AiRunRequest`, `AiRunModel`), the prompt registry (`IPromptsBroker`, use
cases, prompts in rewrite or versioned mode, labels), model prices (`IModelPricesBroker`), the inference log
(`IInferencesBroker`), evals (`IEvalsBroker`, `IEvalSubjectSource`: sets, runs now or queued as pinned batches, status,
cancel, the report), `AiConstants`, the HTTP API's `AiRouteConstants` and `AiScopeConstants`, and the `AiErrors`
catalogue. Calls, sets and runs carry an optional data scope (`tenant:{id}`), so one database holds several tenants. A
product's Domain and Application projects reference this package only; its composition root picks an implementation:

- `WoW2.Sdk.Beta.Be.Ai.Module` — Form 1, in process, over the product's own database.
- `WoW2.Sdk.Beta.Be.Ai.Client` — Forms 2 and 3, over the running Ai service's HTTP API.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Abstractions --prerelease
```
