# codebase/

The code lives in `ai.backend-services/` (.NET) and `ai.frontend-services/` (Vue).

## Backend

- `Ai.BackendServices.slnx` groups `Packages/`, `Services/` and `Tests/`.
- `packages/` holds the NuGet family `WoW2.Sdk.Beta.Be.Ai.*`: `Abstractions` (the contract, `AiRouteConstants`,
  `AiScopeConstants`), `Providers` (named model providers, the one-attempt caller, the provider catalog), `Module`
  (Form 1: brokers, runner, `Images/` (`AiImages` limits and type sniffing, `AiImageLoader` over the host's
  `IAiImageResolver`s and the `http(s)` fetch), `Evals/` with the batch pins, `EvalRunQueue` and `AiEvalWorker`, and
  `Migrations/NNN-name/{Apply,Rollback}.sql`, the named PostgreSQL set `ai` in schema `ai`: `002-ai-layer` the tables
  and the `ai_eval_report_rows` view, `003-seed-prices` the first prices, `004-eval-run-queue` the queue, pins, batches
  and data scopes, `005-eval-run-queue-indexes` their indexes built concurrently in a `-- @no-transaction` file,
  `006-image-inputs` the image counts and hashes on inferences, item image references and pinned batch image hashes),
  `Hosting` (the HTTP API: `Access/` policies and `IAiScopeAccess`, `Endpoints/`, `Reports/`, `Wire/`), `Client`
  (Forms 2/3: `Brokers/` over one named client) and `Testing` (scripted caller + prompts and evals conformance suites).
  `Directory.Build.props` makes them packable by path; the core pin in `Directory.Packages.props` is their floor.
- `service/Ai.Api` hosts the module by `ProjectReference` on `ServiceHost.RunAsync`: `migrate <verb>` and the `ai …`
  operator verbs (`Operator/`) answer before any host boots; otherwise `AddApiDefaults()` / `UseApiDefaults()` with
  authentication and authorization, Identity's token check (`Ai:Auth`), the API (`AddAiApi`/`MapAiApi`, `Ai:Api`), the
  eval worker (`Ai:Worker`), `SystemController` and the pages in `wwwroot/` (filled by the `BuildSpa` target;
  `-p:BuildSpa=false` skips it). A `UserSecretsId` keeps provider keys out of the tree on the local bench.
- Tests: `Unit` (rendering, hashing, resolution, schema check, scoring, prices, sampling, scope grants, report formats,
  the client's wire, image limits and the image loader over a stub handler), `Providers` (the provider package through
  fake clients and stub HTTP, no network — each kind's image block on the wire), `Integration` (the module over
  PostgreSQL with the scripted caller: the queue, worker, pins, cancel, orphans, data scopes, and images through a
  host resolver — bytes, cache, refusals, pinned hashes), `E2E` (the host, its API with real tokens, its worker and its
  verbs in process over PostgreSQL; a run fetching a poster the test host serves), `Conformance`
  (`PromptsBrokerConformance` and `EvalsBrokerConformance` against the module and against the client over the host),
  `Migrations` (apply, Apply → Rollback → Apply, the be sdk lint, idempotency, the upgrades from `003` and `005`, an
  invalid index's rebuild). All but `Unit` and `Providers` need Docker. Run them with `dotnet test Ai.BackendServices.slnx`.

## Frontend

A pnpm workspace: `packages/fe-ai` (`@wow-two-beta/fe-ai`, published: `./contracts` — models, routes and scopes —,
`./kit`, `./admin`) and
`apps/web` (the console that composes `./admin`, private). `scripts/deploy.mjs` copies `apps/web/dist/` to
`ai.backend-services/service/Ai.Api/wwwroot/`.

Rules: wow-two-ws/conventions/development/repo/structure/domain-structure.md
