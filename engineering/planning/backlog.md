# wow-two-sdk-beta.ai — Backlog

*Last updated: 2026-11-17*

Every unbuilt item, grouped; top of each group = next. Shipped work lives in [`version-track/`](version-track/).
Shape: wow-two-ws/conventions/planning/version-track/version-track.md

## Features

| Feature | State | Spec |
|---|---|---|
| Runner, prompts, prices, inference log, evals, verbs | v0.1 | `product/product.md` |
| Queued evals that repeat, with a worker | v0.2 | `engineering/architecture/contract.md` |
| HTTP API, Client brokers (Forms 2/3) and tenant scopes | v0.2 | `engineering/architecture/contract.md` |
| Console pages: prompts, labels, eval reports | planned | — |

## Specced next

ChatGPT builds these in the pull order of wow-two-ws `system/sessions/events-integration/tasks/tasks.md`; each row's spec is its id in the named file there. Delete a row when it ships, with any older row it covers.

| Item | Type | Notes |
|---|---|---|
| A02 Confirm the `afisha-10` labels | product | first eval ran 2026-11-18 (v0.3); only the owner's label confirmation is left · spec in `tasks-platform.md` · owner: ev-ai lane |
| A04 Publish the AI packages and image | engineering | spec in `tasks-platform.md` · owner: ev-ai lane |
| A06 Budget caps and the weekly shadow comparison | feature | spec in `tasks-platform.md` · owner: ev-ai lane |

## Platform

| Item | Type | Notes |
|---|---|---|
| A subject purge also clears its eval items' inputs, failing the batches that pinned them | feature | #8; the worker already refuses a changed input |
| Budgets per use case: monthly USD soft and hard caps from the inference log | feature | SCFD's gate G5 |
| Batch APIs (Anthropic Message Batches, OpenAI Batch) for non-urgent runs | feature | halves the price once paid spend starts |
| LLM-judge scorers (`Microsoft.Extensions.AI.Evaluation.Quality`) behind the scorer seam | feature | OQ-4 |
| Move the core be sdk's LLM folders out; core keeps MCP hosting | chore | in core, after the first publish; sift re-pins to `Ai.Providers` |
| Haven moves from `Haven.Platform.Ai` onto the layer | chore | in Haven's own chat |
