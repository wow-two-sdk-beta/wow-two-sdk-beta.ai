# wow-two-sdk-beta.ai — Contract

*Last updated: 2026-11-17*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract. The C# contract in `packages/Abstractions` is authoritative; this page is its map.

## Brokers

| Broker | Members | Failures |
|---|---|---|
| `IAiRunner` | `RunAsync(AiRunRequest)` → `AiRunModel`: outcome, the answer as JSON text, every attempt as an `InferenceModel`, `FromCache`; `AiRunRequest.Scope` files the call under a data scope | `NotFound` for an unknown use case or no prompt for any routed model; `Validation` for an empty route, a missing variable or a bad scope. A failed, refused or schema-invalid model call is an outcome, never a failure |
| `IPromptsBroker` | `PutUseCaseAsync`, `GetUseCaseAsync`, `ListUseCasesAsync`, `SavePromptAsync(PromptDraft)`, `ListPromptsAsync`, `MoveLabelAsync`, `ListLabelsAsync`, `ResolveAsync(useCase, model, version?, label?)` | `Validation` for a bad key or a label on a rewrite use case; `NotFound` for a missing use case, version or label |
| `IModelPricesBroker` | `PutAsync(ModelPriceModel)`, `ListAsync` | `Validation` for a negative price |
| `IInferencesBroker` | `ListAsync(InferenceQuery)`, `TotalsAsync(InferenceTotalsQuery)` (and `TotalsAsync(from, to)`), `ForgetSubjectAsync(subject, scopes?)` | `Validation` for a limit outside 1–1000 or an empty period |
| `IEvalsBroker` | `PutSetAsync`, `ListSetsAsync`, `GetSetAsync`, `AddItemAsync`, `RelabelItemAsync`, `RemoveItemAsync`, `GetItemAsync`, `ListItemsAsync`, `RunAsync(EvalRunRequest)`, `QueueAsync(EvalMatrixRequest)` → `EvalBatchModel`, `GetRunAsync`, `ListRunsAsync(set)`, `ListRunsAsync(EvalRunQuery)`, `CancelRunAsync`, `RateAsync`, `ReportAsync(set, runIds?, withAnswers)`, `ExportSetAsync`, `ImportSetAsync` | `NotFound` for a missing set, item, run or score; `Validation` for bad JSON, a rating outside 1–5, a sample that keeps nothing, or models × items over the cap; `Conflict` for a put that would move a set to another scope |
| `IEvalSubjectSource` | implemented by a product: `ResolveAsync(useCase, subject)` → prompt variables, called once when an item is added from a subject | — |

## Rules every form keeps

- Prompt modes share `ai_prompts`: `rewrite` updates the row for the same model match in place and keeps its version (1); `versioned` inserts the use case's next version and never updates a row.
- Resolution: the target version is the explicit one, else the label's, else `production`'s, else the newest; among rows at or below it whose model match fits, exact id beats the longest `prefix*`, which beats none; then the highest version.
- The runner tries the route in order, retries transient errors (`AiRetry.IsTransient`) on the same model, then moves on after a failure, a refusal or a schema-invalid answer; each attempt is one `ai_inferences` row priced from the `ai_model_prices` row in effect.
- An identical call (use case, prompt hash, provider, model, input hash) in the same data scope answers from the stored inference unless the request is `Fresh`; another scope's answer is never reused.
- An eval run is one prompt version × one model with no fallback; a score compares the batch's pinned `ScoreFields` case-insensitively and marks a miss severe when it is in the pinned `SevereMisses`.
- Repeatable comparisons: a batch (`QueueAsync`, and `RunAsync` as a batch of one) pins, when it is created, the sampled items in run order with each one's expected answer and input hash, and the set's scoring rules; each run pins its cell's resolved prompt — row id, version, content hash, templates, parameters and the use case's output schema. A run sends exactly that, whatever a later rewrite, relabel or label move did; a run whose pinned input changed or was purged fails with the reason instead of running. Report rows carry the pinned prompt hash and the pinned expected answer.
- Sampling: `ItemIds`, then `Tags` (all of them), then `Count` items ranked by a hash of `Seed` (default 0) and each item's id, in the set's order.
- The queue: a run is `queued` → `running` → `succeeded`, `failed` or `canceled`. A worker claims with `FOR UPDATE SKIP LOCKED` (never twice), counts items done and beats; a cancel stops a running run between items, keeping what it scored; a run with no heartbeat for `OrphanAfter` fails as orphaned; a provider failing every item still completes the run, each item recorded as failed. A request over `MaxEvalItemCalls` (default 2,000) models × items is refused before anything is queued.
- Data scopes: `Scope` (`tenant:{id}`; none for the unscoped bucket) sits on every inference, eval set, batch and run; a set's runs and calls take the set's scope, and a set never moves scope. Filters take `Scopes`: none keeps every scope, `AiConstants.Unscoped` (`""`) names the unscoped bucket, an empty list keeps nothing. Use cases, prompts, labels and prices are shared.
- Report spend: `CostMicroUsd` counts cached answers at their original cost, `SpentMicroUsd` only new calls; `Unpriced` counts items whose model had no price, so a cost of 0 there is unknown, not free.
- Costs are integer micro-USD.

| Form | Implementation | Wire |
|---|---|---|
| 1 | the module's brokers (`AddAi()` beside `AddAiProviders(configuration)`); `AddAiEvalWorker()` runs queued evals in the host, opt-in | in process, over the consumer's PostgreSQL schema `ai` |
| 2/3 | the Client's HTTP brokers (`AddAiClient`) for all five brokers, over the Hosting package's API | HTTP under `api/ai/` |
| verbs | `service/Ai.Api`'s `ai …` operator verbs, `ai eval queue|status|cancel` and `ai providers [--live]` among them | one JSON line per verb; `eval report --format md|csv` prints a table |
| fe | `fe-ai/contracts` mirrors the models, the routes and the scopes; `fe-ai/admin` `EvalReportView` renders a report | no HTTP client yet |

## HTTP mirror

`AddAiApi()` / `MapAiApi()` (`packages/Hosting`) map the brokers under `api/ai/` (`AiRouteConstants`). Bodies carry the
payload under `data` on the be sdk's JSON preset (camelCase, enums as camelCase words, dictionary keys as written);
failures are the be sdk's problem details, `code` naming the `AppErrorType`, which the Client turns back into the same
error.

| Scope | Routes |
|---|---|
| `ai.read` | `GET providers[?live=true]`, `usecases`, `usecases/{key}`, `…/prompts`, `…/prompts/resolve`, `…/labels`, `prices`, `inferences`, `inferences/totals`, `evals/sets`, `evals/sets/{key}`, `…/runs`, `…/report?runs=&answers=&format=json|csv|md`, `evals/runs`, `evals/runs/{id}` |
| `ai.run` | `POST runs` (one synchronous run), `POST inferences/forget` |
| `ai.prompts-manage` | `PUT usecases/{key}`, `POST …/prompts`, `PUT …/labels/{label}`, `PUT prices` |
| `ai.evals-manage` | `PUT evals/sets/{key}`, `GET`/`POST …/items`, `GET`/`DELETE evals/items/{id}`, `PUT evals/items/{id}/expected`, `POST evals/sets/{key}/runs` (`202`, the batch with its run ids), `POST evals/runs` (now), `POST evals/runs/{id}/cancel`, `POST evals/runs/{id}/ratings`, `GET …/export`, `POST evals/import` |

- No token answers 401, a token without the route's scope 403; item inputs and exports need `ai.evals-manage`, never the generic read. `Ai:Api:AllowAnonymousInDevelopment` lifts the scopes in Development only.
- The host's `IAiScopeAccess` grants each caller its data scopes (default: the unscoped bucket only). Lists keep the granted scopes (`?scope=` narrows, repeatable); a set, item or run outside them answers 404; a write naming another scope 403; a write naming none lands in the caller's default scope.

- Conformance: `PromptsBrokerConformance` and `EvalsBrokerConformance` (`packages/Testing`) run against the module and against the Client over the service host in `tests/Ai.Tests.Conformance`, and against the released image in `conformance.yml`.
- Providers: `IAiModelCaller` (`packages/Providers`) is the module's seam to the models; `ScriptedAiModelCaller` (`packages/Testing`) replaces it in tests. `IAiProviderCatalog` lists the configured providers (never a key) and asks one for its live model ids.
