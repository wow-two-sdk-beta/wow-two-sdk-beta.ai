# wow-two-sdk-beta.ai — Capability spec

*Last updated: 2026-11-13*

> The capability's definition: what it does, for whom, in which forms. State and decisions: `context.md`; one spec per
> feature group in `features/`. Rules: wow-two-ws/conventions/development/capability-forms.md.

## Problem

- Several products call language models: SCFD classifies harvested posts, Haven classifies listings, sift scores videos, and Ocharo's events need kinds and fields.
- Each call needs the same machinery: provider keys, a fallback provider, prompts that change without a deploy, a record of every call's tokens and cost, and a way to compare prompts and models before switching.
- The layer owns state (prompts with their versions, the inference log, prices, eval sets and results) and secrets (provider keys), so it is a domain, not a core helper (wow-two-ws `ideas/ai-layer-analysis.md`, OQ-1).

## Forms

| Form | Ships | Named consumer | State |
|---|---|---|---|
| 1 · library | `WoW2.Sdk.Beta.Be.Ai.Module` + `…Ai.Providers` | SCFD (`wow-two-sdk-beta.harvest`), Haven | v0.1 |
| 2 · self-hosted service | `…Ai.Client` + the `ai` image | the local eval bench | the image runs the verbs; HTTP brokers later |
| 3 · provided service | `…Ai.Client` | — | backlog |

## Model

| Noun | What it is |
|---|---|
| use case | one AI job (`ocharo-relevance`): its prompt mode, output schema and model route |
| prompt | one row of `ai_prompts`: templates and parameters for every model or for a model match |
| prompt mode | `rewrite`: a save updates the row and keeps its version; `versioned`: a save inserts the next version and labels pick one |
| label | a movable pointer at a version (`production`, `candidate`) |
| route | the models a use case tries in order, `{provider}/{model}`; a later one runs when an earlier one fails |
| inference | one model attempt: prompt version and hash, model, tokens, cost, latency, outcome |
| price | a model's price per 1M tokens from a date on |
| eval set | frozen inputs with expected answers, score fields and severe-miss rules |
| eval run | one prompt version × one model over a set, scored per item |
| report | runs joined with scores, inferences and prices: accuracy, severe misses, cost per item, latency, ratings |

- Verbs: run a use case; save, list and label prompts; put prices; add, run, rate, report, export and import evals.
- Model failures are outcomes, not errors: a run returns every attempt with its cost, whatever happened.

## Phases

| Phase | Version | Holds |
|---|---|---|
| P1 | v0.1 | providers, prompts in both modes, the inference log with prices, evals with the report, operator verbs, the local bench; SCFD's relevance step is its first consumer |
| P2 | — | HTTP API and the Client brokers (Forms 2/3), console pages for prompts and reports, budgets per use case |
| P3 | — | batch APIs, LLM-judge scorers, Haven's move onto the layer |

## Sources

- wow-two-ws `ideas/ai-layer-analysis.md` — the analysis and target design
- wow-two-ws `ideas/ai-classification-providers.md` — the provider pick, prices and the eval plan
- wow-two-ws `system/sessions/events-integration/events-integration.md` #54, #55, #64
