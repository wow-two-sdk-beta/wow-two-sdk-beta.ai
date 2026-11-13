# File References

> **Lookup table only.** Do NOT read files proactively — only when the current task requires them.
> **Tracks docs outside `engineering/codebase/`.** Source files are navigated via `tree`/`find`/`grep`, never listed here.

| Question about | Read |
|---|---|
| The capability — problem, forms, model, phases | `product/product.md` |
| Name and one-line description (read by consoles) | `product/product.json` |
| State, tasks, decisions | `product/context.md` |
| Feature list + state | `engineering/planning/backlog.md` § Features |
| One feature's spec | `product/features/{feature}.md` |
| User / product flows | `product/flows/flows.md` |
| Hosted pages' brand | `product/brand/brand.md` |
| Technical overview · stack · map | `engineering/engineering.md` |
| System design — forms map, package graph, service role | `engineering/architecture/architecture.md` |
| The broker contract, every form | `engineering/architecture/contract.md` |
| Packages, layers, consumers | `engineering/architecture/package-registry.md` |
| Architecture decisions | `engineering/architecture/decisions/decisions.md` |
| What lives where in the code | `engineering/codebase/codebase.md` |
| Backend dev guidelines | `engineering/development/backend-guidelines.md` |
| Frontend dev guidelines | `engineering/development/frontend-guidelines.md` |
| Iteration / version workflow | `engineering/development/iteration-guide.md` |
| Backlog — every unbuilt item | `engineering/planning/backlog.md` |
| Active version | newest `engineering/planning/version-track/v{X.Y}/v{X.Y}.md` |
| Working rules for agents | `engineering/development/rules.md` |
| Repository — how it was created, GitHub settings, rollout state | `engineering/development/development.md` § Repository |
| Deploy / ops | `engineering/deployment/deployment.md` |
| What deploys — service, sites, settings, dev check | `engineering/deployment/deploy.yml` |
| Releases — versions, tags, verification | `engineering/scripts/scripts.md` |
| Research and analysis batches | `engineering/research/research.md` |

## Source projects (`engineering/codebase/`)

> Individual files NOT listed — use `tree`/`find`/`grep`. Describe projects only.

### `codebase/ai.backend-services/`
| Project | What it is |
|---|---|
| `packages/Abstractions` | the contract: `IAiRunner`, `IPromptsBroker`, `IModelPricesBroker`, `IInferencesBroker`, `IEvalsBroker`, `IEvalSubjectSource`, models, constants, errors |
| `packages/Providers` | named model providers (Anthropic, OpenAI-compatible, Ollama) from `Ai:Providers`, `IAiModelCaller`, `IAiChatClients`, `AddAiProviders()` |
| `packages/Module` | Form 1: the brokers and the runner in process, `AddAi()`, the named PostgreSQL set `ai` of embedded SQL migrations (tables, report view, seeded prices) |
| `packages/Client` | Forms 2/3: `AddAiClient()`; the HTTP brokers land with the API |
| `packages/Testing` | `ScriptedAiModelCaller` and `PromptsBrokerConformance` |
| `service/Ai.Api` | the deployable host: the module, the `ai …` operator verbs (the eval bench), status, the console |
| `tests/Ai.Tests.{Unit,Providers,Integration,E2E,Conformance,Migrations}` | the test tiers |

### `codebase/ai.frontend-services/`
| Project | What it is |
|---|---|
| `packages/fe-ai` | `@wow-two-beta/fe-ai`: `./contracts` mirror, `./kit` (cost and latency formatters), `./admin` (`EvalReportView`) |
| `apps/web` | the console (`/`, composing `./admin`), served by `Ai.Api` |
