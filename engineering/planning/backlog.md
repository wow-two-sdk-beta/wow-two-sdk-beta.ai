# wow-two-sdk-beta.ai — Backlog

*Last updated: 2026-11-13*

Every unbuilt item, grouped; top of each group = next. Shipped work lives in [`version-track/`](version-track/).
Shape: wow-two-ws/conventions/planning/version-track/version-track.md

## Features

| Feature | State | Spec |
|---|---|---|
| Runner, prompts, prices, inference log, evals, verbs | v0.1 | `product/product.md` |
| HTTP API and Client brokers (Forms 2/3) | planned | — |
| Console pages: prompts, labels, eval reports | planned | — |

## Specced next

ChatGPT builds these in the pull order of wow-two-ws `system/sessions/events-integration/tasks/tasks.md`; each row's spec is its id in the named file there. Delete a row when it ships, with any older row it covers.

| Item | Type | Notes |
|---|---|---|
| A01 Make queued eval runs repeatable | fix | spec in `tasks-platform.md` · owner: ev-ai lane |
| A02 First eval on `afisha-10` from the bench | product | spec in `tasks-platform.md` · owner: ev-ai lane |
| A03 First HTTP slice: providers, queue, status, cancel, report | feature | spec in `tasks-platform.md` · owner: ev-ai lane |
| A04 Publish the AI packages and image | engineering | spec in `tasks-platform.md` · owner: ev-ai lane |
| A06 Budget caps and the weekly shadow comparison | feature | spec in `tasks-platform.md` · owner: ev-ai lane |

## Platform

| Item | Type | Notes |
|---|---|---|
| HTTP controllers for every broker, then `ApiPromptsBroker`, `ApiEvalsBroker` and the rest in `packages/Client`, with the conformance suites run against the client | feature | the Client package ships only `AddAiClient` in v0.1 |
| Budgets per use case: monthly USD soft and hard caps from the inference log | feature | SCFD's gate G5 |
| Batch APIs (Anthropic Message Batches, OpenAI Batch) for non-urgent runs | feature | halves the price once paid spend starts |
| LLM-judge scorers (`Microsoft.Extensions.AI.Evaluation.Quality`) behind the scorer seam | feature | OQ-4 |
| Move the core be sdk's LLM folders out; core keeps MCP hosting | chore | in core, after the first publish; sift re-pins to `Ai.Providers` |
| Haven moves from `Haven.Platform.Ai` onto the layer | chore | in Haven's own chat |
