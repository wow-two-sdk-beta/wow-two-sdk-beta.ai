# wow-two-sdk-beta.ai — Product Context

*Last updated: 2026-11-14*

> Current state — changes often. Durable definition: `product.md`.

## Current state

Paused 2026-11-14 at the developer's weekly usage limit; resumes Tuesday 2026-11-18. v0.1's first slice is committed and pushed (`aaab511`: providers, prompts in both modes, the inference log with prices, evals with the report, operator verbs; 260 backend tests). The HTTP API with queued eval runs is half built and uncommitted in the tree; the first eval waits for the developer's API keys. The repo is public; packages are not published yet.

## Active tasks

| Task | Status | Notes |
|---|---|---|
| HTTP API, queued eval runs, worker, sampling | paused, partly built, uncommitted | version track v0.1 Iteration 2 |
| First 10-post eval for SCFD's `ocharo-relevance` | waits for API keys | version track v0.1 Iteration 3 |
| Publish: ruleset, NuGet policy, npm first version | waits for the developer's hand steps | version track v0.1 Iteration 4 |
| SCFD's relevance step on the layer | todo | after the first eval |

## Open questions

- OQ-2 the provided AI service (Form 3) with keys and budgets in one place — later
- OQ-4 LLM-judge scorers from `Microsoft.Extensions.AI.Evaluation` — later, wrapped as scorers

## Decisions

- 2026-11-14 — the deployed services take evals over HTTP: queued runs, a worker, status in a table, sample size per run; the CLI stays for local and ops (developer)
- 2026-11-13 — one domain repo holds providers and the stateful layer; core keeps only MCP hosting (developer: "yes let's extract it and let's get it working")
- 2026-11-13 — prompts live in tables only; one table serves both modes, a rewrite keeps the version, a versioned save inserts the next one (developer)
- 2026-11-13 — eval inputs and model answers stay in databases; a repo records per-run summaries only (developer: "we don't commit to the code directly, maybe just add pricing per post and accuracy")
