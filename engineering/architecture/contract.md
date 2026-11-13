# wow-two-sdk-beta.ai — Contract

*Last updated: 2026-11-13*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract. The C# contract in `packages/Abstractions` is authoritative; this page is its map.

## Brokers

| Broker | Members | Failures |
|---|---|---|
| `IAiRunner` | `RunAsync(AiRunRequest)` → `AiRunModel`: outcome, the answer as JSON text, every attempt as an `InferenceModel`, `FromCache` | `NotFound` for an unknown use case or no prompt for any routed model; `Validation` for an empty route or a missing variable. A failed, refused or schema-invalid model call is an outcome, never a failure |
| `IPromptsBroker` | `PutUseCaseAsync`, `GetUseCaseAsync`, `ListUseCasesAsync`, `SavePromptAsync(PromptDraft)`, `ListPromptsAsync`, `MoveLabelAsync`, `ListLabelsAsync`, `ResolveAsync(useCase, model, version?, label?)` | `Validation` for a bad key or a label on a rewrite use case; `NotFound` for a missing use case, version or label |
| `IModelPricesBroker` | `PutAsync(ModelPriceModel)`, `ListAsync` | `Validation` for a negative price |
| `IInferencesBroker` | `ListAsync(InferenceQuery)`, `TotalsAsync(from, to)`, `ForgetSubjectAsync(subject)` | `Validation` for a limit outside 1–1000 |
| `IEvalsBroker` | `PutSetAsync`, `ListSetsAsync`, `AddItemAsync`, `RelabelItemAsync`, `RemoveItemAsync`, `ListItemsAsync`, `RunAsync(EvalRunRequest)`, `ListRunsAsync`, `RateAsync`, `ReportAsync(set, runIds?, withAnswers)`, `ExportSetAsync`, `ImportSetAsync` | `NotFound` for a missing set, item, run or score; `Validation` for bad JSON or a rating outside 1–5 |
| `IEvalSubjectSource` | implemented by a product: `ResolveAsync(useCase, subject)` → prompt variables, called once when an item is added from a subject | — |

## Rules every form keeps

- Prompt modes share `ai_prompts`: `rewrite` updates the row for the same model match in place and keeps its version (1); `versioned` inserts the use case's next version and never updates a row.
- Resolution: the target version is the explicit one, else the label's, else `production`'s, else the newest; among rows at or below it whose model match fits, exact id beats the longest `prefix*`, which beats none; then the highest version.
- The runner tries the route in order, retries transient errors (`AiRetry.IsTransient`) on the same model, then moves on after a failure, a refusal or a schema-invalid answer; each attempt is one `ai_inferences` row priced from the `ai_model_prices` row in effect.
- An identical call (use case, prompt hash, provider, model, input hash) answers from the stored inference unless the request is `Fresh`.
- An eval run is one prompt version × one model with no fallback; a score compares the set's `ScoreFields` case-insensitively and marks a miss severe when it is in `SevereMisses`.
- Costs are integer micro-USD.

| Form | Implementation | Wire |
|---|---|---|
| 1 | the module's brokers (`AddAi()` beside `AddAiProviders(configuration)`) | in process, over the consumer's PostgreSQL schema `ai` |
| 2/3 | none yet; `AddAiClient` registers the named client only | the HTTP API lands with the Client brokers (backlog) |
| verbs | `service/Ai.Api`'s `ai …` operator verbs | one JSON line per verb; `eval report --format md|csv` prints a table |
| fe | `fe-ai/contracts` mirrors the models; `fe-ai/admin` `EvalReportView` renders a report | no HTTP client yet |

- Conformance: `PromptsBrokerConformance` (`packages/Testing`) runs against the module in `tests/Ai.Tests.Conformance`; the client joins it with the HTTP brokers.
- Providers: `IAiModelCaller` (`packages/Providers`) is the module's seam to the models; `ScriptedAiModelCaller` (`packages/Testing`) replaces it in tests.
