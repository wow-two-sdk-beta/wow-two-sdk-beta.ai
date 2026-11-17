# wow-two-sdk-beta.ai — Product Context

*Last updated: 2026-11-17*

> Current state — changes often. Durable definition: `product.md`.

## Current state

v0.1 shipped and `10.0.0-beta` is on nuget.org (`d71898b`). v0.2 is built in the tree, uncommitted (2026-11-17): queued evals that repeat exactly (a batch pins its sample, expected answers and scoring, each run its prompt) with an opt-in worker, data scopes for tenants in one database, the HTTP API package (`Ai.Hosting`) with `ai.*` scopes, the Client's five HTTP brokers, and the evals conformance suite on module and client. The first eval waits for the developer's API keys.

## Active tasks

| Task | Status | Notes |
|---|---|---|
| HTTP API, queued eval runs, worker, sampling, tenant scopes | built, uncommitted; release open | version track v0.2 |
| First 10-post eval for SCFD's `ocharo-relevance` | waits for API keys | backlog A02 |
| Publish: ruleset, NuGet policy, npm first version | waits for the developer's hand steps | backlog A04, v0.2 Iteration 5 |
| SCFD's relevance step on the layer | todo | after the first eval |

## Open questions

- OQ-2 the provided AI service (Form 3) with keys and budgets in one place — later
- OQ-4 LLM-judge scorers from `Microsoft.Extensions.AI.Evaluation` — later, wrapped as scorers

## Decisions

- 2026-11-17 — the AI service deploys after the API credit cycle ends (2026-11-24); until then SCFD runs the layer in process (Form 1) and later switches to the client by configuration; the service is for keys, prompts, evals and budgets in one place, not for compute load (developer: "agreed on after")
- 2026-11-17 — the cycle's API credit goes to any useful work, about 70% Ocharo and 30% Haven-related SCFD processing, with no spending caps built for it (developer: "it's ok to spend all on anything just smth useful")
- 2026-11-17 — the work stays in SCFD: OLX and AI classification improve there; Haven is untouched and later consumes SCFD like any product (developer)
- 2026-11-14 — the deployed services take evals over HTTP: queued runs, a worker, status in a table, sample size per run; the CLI stays for local and ops (developer)
- 2026-11-13 — one domain repo holds providers and the stateful layer; core keeps only MCP hosting (developer: "yes let's extract it and let's get it working")
- 2026-11-13 — prompts live in tables only; one table serves both modes, a rewrite keeps the version, a versioned save inserts the next one (developer)
- 2026-11-13 — eval inputs and model answers stay in databases; a repo records per-run summaries only (developer: "we don't commit to the code directly, maybe just add pricing per post and accuracy")
