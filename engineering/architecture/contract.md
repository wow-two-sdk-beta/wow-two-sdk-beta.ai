# wow-two-sdk-beta.ai — Contract

*Last updated: 2026-11-18*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract. The C# contract in `packages/Abstractions` is authoritative; this page is its map.

## Brokers

| Broker | Members | Failures |
|---|---|---|
| `IAiRunner` | `RunAsync(AiRunRequest)` → `AiRunModel`: outcome, the answer as JSON text, every attempt as an `InferenceModel`, `FromCache`; `AiRunRequest.Scope` files the call under a data scope; `AiRunRequest.Images` carries `AiImageRef`s; `AiRunRequest.CheckEnums` false lets a top-level value outside its `enum` pass, for a caller that repairs it | `NotFound` for an unknown use case, no prompt for any routed model, or an image no resolver reads; `Validation` for an empty route, a missing variable, a bad scope, or an image over the limits. A failed, refused or schema-invalid model call is an outcome, never a failure |
| `IPromptsBroker` | `PutUseCaseAsync`, `GetUseCaseAsync`, `ListUseCasesAsync`, `SavePromptAsync(PromptDraft)`, `ListPromptsAsync`, `MoveLabelAsync`, `ListLabelsAsync`, `ResolveAsync(useCase, model, version?, label?)` | `Validation` for a bad key or a label on a rewrite use case; `NotFound` for a missing use case, version or label |
| `IModelPricesBroker` | `PutAsync(ModelPriceModel)`, `ListAsync` | `Validation` for a negative price |
| `IInferencesBroker` | `ListAsync(InferenceQuery)`, `TotalsAsync(InferenceTotalsQuery)` (and `TotalsAsync(from, to)`), `ForgetSubjectAsync(subject, scopes?)` | `Validation` for a limit outside 1–1000 or an empty period |
| `IEvalsBroker` | `PutSetAsync`, `ListSetsAsync`, `GetSetAsync`, `AddItemAsync`, `RelabelItemAsync`, `RemoveItemAsync`, `GetItemAsync`, `ListItemsAsync`, `RunAsync(EvalRunRequest)`, `QueueAsync(EvalMatrixRequest)` → `EvalBatchModel`, `GetRunAsync`, `ListRunsAsync(set)`, `ListRunsAsync(EvalRunQuery)`, `CancelRunAsync`, `RateAsync`, `ReportAsync(set, runIds?, withAnswers)`, `ExportSetAsync`, `ImportSetAsync` | `NotFound` for a missing set, item, run or score; `Validation` for bad JSON, a rating outside 1–5, a sample that keeps nothing, or models × items over the cap; `Conflict` for a put that would move a set to another scope |
| `IEvalSubjectSource` | implemented by a product: `ResolveAsync(useCase, subject)` → prompt variables, called once when an item is added from a subject; a source that also returns images overrides the default-implemented `ResolveInputAsync` → `EvalSubjectInput` (variables + `AiImageRef`s) | — |
| `IAiImageResolver` | implemented by a host for images behind its own keys (SCFD: `fss:{id}`): `ResolveAsync(AiImageRef)` → `AiImageContent` (bytes + media type), or none when it does not handle the reference | — |

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
- Images: an `AiImageRef` names exactly one of `Uri` (absolute `http(s)`) and `Key` (what a host's `IAiImageResolver`
  reads), with an optional `MediaType` and `Sha256`. A prompt row's `PromptParameters.MaxImages` (0 or none = text only)
  takes the first that many of a call's images; a prompt that takes none never resolves them and its input hash leaves
  them out. One that takes images resolves them before any call — the host's resolvers in registration order, then the
  module's own `http(s)` fetch (`AiModuleConstants.ImageHttpClientName`, `ImageFetchTimeout`) — and sends each as a
  `DataContent` before the user text. Limits are module options: `MaxImagesPerCall` (4), `MaxImageBytes` (5 MB),
  `ImageMediaTypes` (JPEG, PNG, WebP, GIF; the bytes decide the type), each refused as `Validation` before any call; a
  `Sha256` that no longer matches is refused too.
- Image hashes: each sent image's SHA-256 joins the attempt's input hash, so the cache tells images apart, and lands on
  `InferenceModel.ImageCount` / `ImageHashes` (`ai_inferences`); bytes are never stored. Eval items keep references only
  (`EvalItemModel.Images`, `EvalItemDraft.Images`, the set export); a batch whose prompts take images resolves each item's
  images once and pins their hashes; a run resolves them again and fails, as for a changed input, when one no longer
  matches or cannot be read, before that item reaches a model.
- Providers: Anthropic sends base64 `image` blocks, OpenAI-compatible hosts `image_url` parts with a `data:` URL,
  Ollama the message's base64 `images`; image tokens arrive inside each provider's input tokens. A model that takes no
  image answers its provider's 400, classified `BadRequest`.

| Form | Implementation | Wire |
|---|---|---|
| 1 | the module's brokers (`AddAi()` beside `AddAiProviders(configuration)`); `AddAiEvalWorker()` runs queued evals in the host, opt-in | in process, over the consumer's PostgreSQL schema `ai` |
| 2/3 | the Client's HTTP brokers (`AddAiClient`) for all five brokers, over the Hosting package's API | HTTP under `api/ai/` |
| verbs | `service/Ai.Api`'s `ai …` operator verbs, `ai eval queue|status|cancel`, `ai providers [--live]`, `ai run --image <uri|key>`, `ai eval add --image …` and `ai prompt save --max-images` among them | one JSON line per verb; `eval report --format md|csv` prints a table |
| fe | `fe-ai/contracts` mirrors the models (`AiImageRef`, `AiRunRequest`, `EvalItemDraft` among them), the routes and the scopes; `fe-ai/admin` `EvalReportView` renders a report | no HTTP client yet |

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
- Providers: `IAiModelCaller` (`packages/Providers`) is the module's seam to the models; `ScriptedAiModelCaller` (`packages/Testing`) replaces it in tests and records each `AiCallRequest`, its image bytes included. `IAiProviderCatalog` lists the configured providers (never a key) and asks one for its live model ids.
- Image URIs over HTTP: the module fetches an `http(s)` URI only when its host is listed in `Ai:Module:ImageFetchHosts` (exact, or `*.` for subdomains); the list starts empty, so a caller with `ai.run` or `ai.evals-manage` cannot make the host request an arbitrary or internal URL. Send public or presigned URLs from listed hosts; a private key resolves only where its host's `IAiImageResolver` is registered — in process today, not yet in the service.
