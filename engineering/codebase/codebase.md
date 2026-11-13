# codebase/

The code lives in `ai.backend-services/` (.NET) and `ai.frontend-services/` (Vue).

## Backend

- `Ai.BackendServices.slnx` groups `Packages/`, `Services/` and `Tests/`.
- `packages/` holds the NuGet family `WoW2.Sdk.Beta.Be.Ai.*`: `Abstractions` (the contract), `Providers` (named model
  providers and the one-attempt caller), `Module` (Form 1, with `Migrations/NNN-name/{Apply,Rollback}.sql`, the named
  PostgreSQL set `ai` in schema `ai`: `002-ai-layer` the tables and the `ai_eval_report_rows` view, `003-seed-prices`
  the first prices), `Client` (Forms 2/3) and `Testing` (scripted caller + conformance suite). `Directory.Build.props`
  makes them packable by path; the core pin in `Directory.Packages.props` is their floor.
- `service/Ai.Api` hosts the module by `ProjectReference` on `ServiceHost.RunAsync`: `migrate <verb>` and the `ai …`
  operator verbs (`Operator/`) answer before any host boots; otherwise `AddApiDefaults()` / `UseApiDefaults()`,
  `SystemController` and the pages in `wwwroot/` (filled by the `BuildSpa` target; `-p:BuildSpa=false` skips it).
  A `UserSecretsId` keeps provider keys out of the tree on the local bench.
- Tests: `Unit` (rendering, hashing, resolution, schema check, scoring, prices), `Providers` (the provider package
  through fake clients and stub HTTP, no network), `Integration` (the module over PostgreSQL with the scripted caller),
  `E2E` (the host and its verbs in process over PostgreSQL), `Conformance` (`PromptsBrokerConformance` against the
  module), `Migrations` (apply, Apply → Rollback → Apply, the be sdk lint, idempotency). All but `Unit` and `Providers`
  need Docker. Run them with `dotnet test Ai.BackendServices.slnx`.

## Frontend

A pnpm workspace: `packages/fe-ai` (`@wow-two-beta/fe-ai`, published: `./contracts`, `./kit`, `./admin`) and
`apps/web` (the console that composes `./admin`, private). `scripts/deploy.mjs` copies `apps/web/dist/` to
`ai.backend-services/service/Ai.Api/wwwroot/`.

Rules: wow-two-ws/conventions/development/repo/structure/domain-structure.md
