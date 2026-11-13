# wow-two-sdk-beta.ai — Product Context

*Last updated: 2026-11-13*

> Current state — changes often. Durable definition: `product.md`.

## Current state

v0.1 is in build: the repo was created from the domain template on 2026-11-13, the contract is written, and the providers, the module with evals and verbs, and the frontend's first slice are landing. Packages are not published yet; the repo is private until the developer's public flip.

## Active tasks

| Task | Status | Notes |
|---|---|---|
| v0.1 build | in progress | `engineering/planning/version-track/v0.1/v0.1.md` |
| First 10-post eval for SCFD's `ocharo-relevance` | todo | after v0.1 runs locally; inputs stay outside git |

## Open questions

- OQ-2 the provided AI service (Form 3) with keys and budgets in one place — later
- OQ-4 LLM-judge scorers from `Microsoft.Extensions.AI.Evaluation` — later, wrapped as scorers

## Decisions

- 2026-11-13 — one domain repo holds providers and the stateful layer; core keeps only MCP hosting (developer: "yes let's extract it and let's get it working")
- 2026-11-13 — prompts live in tables only; one table serves both modes, a rewrite keeps the version, a versioned save inserts the next one (developer)
- 2026-11-13 — eval inputs and model answers stay in databases; a repo records per-run summaries only (developer: "we don't commit to the code directly, maybe just add pricing per post and accuracy")
