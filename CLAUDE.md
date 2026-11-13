# wow-two-sdk-beta.ai

WoW2 AI: routes model calls across providers with fallback, keeps prompts and their versions in tables, logs every inference with its tokens and cost, and runs evals that compare prompts and models on one report.

## Lazy loading

- Do NOT pre-read files at conversation start; open a file only when the task needs it.
- `.claude/rules/file-references.md` is a lookup table, not a reading list.
- Navigate code under `engineering/codebase/` with `tree`/`find`/`grep` — it is not listed in references.

## Structure

```
product/                 ← the capability (no code): product · context · features · flows · brand
engineering/             ← the HOW
├── architecture/        ← architecture · contract · package-registry · decisions/ · flows/
├── development/         ← this repo's dev guidelines (defer to wow-two-ws/conventions/)
├── planning/            ← backlog.md + version-track/v{X.Y}/ (newest = active; drives the image version only)
├── deployment/          ← deploy.yml · Dockerfile · docker-compose.yml · deployment.md
├── research/ · scripts/ ← analyses · release helpers publish-packages.yml calls
└── codebase/            ← THE CODE — only place code lives
    ├── ai.backend-services/    ← Ai.BackendServices.slnx: packages/{Abstractions,Providers,Module,Client,Testing} · service/Ai.Api · tests/Ai.Tests.*
    └── ai.frontend-services/   ← pnpm workspace: packages/fe-ai (npm: contracts · kit · admin) · apps/web (console)
```

## Rules

- Shape: wow-two-ws/conventions/development/repo/structure/domain-structure.md; repo-specific deltas only in `engineering/development/`.
- The contract lives in `packages/Abstractions`; Module (Form 1) implements it over `packages/Providers`, and
  `Ai.Tests.Conformance` runs `PromptsBrokerConformance` (`packages/Testing`) against it; the Client's HTTP brokers
  join with the HTTP API.
- Prompts live in tables only (`ai_prompts`), in `rewrite` or `versioned` mode; never add a prompt file or a prompt
  constant to code. A product seeds its first prompt through the verbs or `IPromptsBroker`.
- Eval inputs and model answers stay in databases; a repo records only per-run summaries (accuracy, severe misses, cost
  per item, latency). Harvested text never enters git.
- Provider keys live in configuration (`Ai:Providers:{name}:ApiKey`): user-secrets locally, a Wheelhouse secret on a
  target; never in a file in the tree.
- A package references only what its layer row allows; the service takes the packages by `ProjectReference` and adds
  only hosting, the operator verbs, the system status and the console.
- Management screens ship in `fe-ai`'s `./admin` export; `apps/web`'s console composes them and builds none itself.
- Package versions come from tags (`be-v*` NuGet, `fe-v*` npm) in `publish-packages.yml`; never commit a version,
  never create a GitHub Release for a package tag. The image version comes from the version track (`v*`).
- Core is pinned (`BackendSdkVersion`, the exact `@wow-two-beta/fe`); a needed core change goes to core first, then the
  pin moves here. `core-latest.yml` tests the newest core nightly; a red run is a fix here, not a gate.
- The store is PostgreSQL. Schema changes are `packages/Module/Migrations/NNN-name/{Apply,Rollback}.sql` pairs of the
  module's named set `ai` (schema `ai`); never edit an applied one. A deploy applies them with the image's
  `migrate` verb before the release serves; only a local run applies them at boot.
- The backend build runs the frontend build (`BuildSpa`), so it needs Node and pnpm; every tier but unit needs Docker
  (PostgreSQL through Testcontainers).
- Repository creation, GitHub settings, rollout state and hand steps: `engineering/development/development.md`
  § Repository.
