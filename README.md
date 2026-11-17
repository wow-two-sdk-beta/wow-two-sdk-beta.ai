# wow-two-sdk-beta.ai

WoW2 AI: routes model calls across providers with fallback, keeps prompts and their versions in tables, logs every inference with its tokens and cost, and runs evals that compare prompts and models on one report.

## Artifacts

| Artifact | Registry | Line · tag | Install |
|---|---|---|---|
| `WoW2.Sdk.Beta.Be.Ai.Abstractions` — the contract | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Abstractions --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Providers` — named model providers and the one-attempt caller | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Providers --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Module` — Form 1, in process | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Module --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Hosting` — the HTTP API any host mounts (`AddAiApi`, `MapAiApi`) | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Hosting --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Client` — Forms 2 and 3, the five brokers over HTTP | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Client --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Testing` — scripted model caller and the prompts and evals conformance suites | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Testing --prerelease` |
| `@wow-two-beta/fe-ai` — contract mirror, formatters, eval report view | npm | `0.0.z` · `fe-v*` | `pnpm add @wow-two-beta/fe-ai` |
| `ai` service image | GHCR | `X.Y.Z` · `v*` | `ghcr.io/wow-two-sdk-beta/wow-two-sdk-beta.ai/ai`, deployed by Wheelhouse |

A product pins the family with one `AiSdkVersion` property beside `BackendSdkVersion`.

## Forms

```csharp
// Form 1 — in process, over the product's own PostgreSQL database; its migrate verbs apply the module's set.
// Providers come from Ai:Providers:{name} (Kind, BaseUrl, ApiKey, TimeoutSeconds); keys from user-secrets or a target secret.
services.AddAiProviders(configuration);
services.AddAi().Configure(options => options.ConnectionString = "...");

services.AddAiEvalWorker();   // optional: runs queued evals in this host

// Then any product code runs a use case; its prompt lives in the ai_prompts table, its route in ai_use_cases.
var run = await runner.RunAsync(new AiRunRequest
{
    UseCase = "ocharo-relevance", Variables = variables, Subject = subject, Scope = $"tenant:{tenantId}",
});
// run.Outcome, run.Output (JSON), run.Attempts: model, tokens, cost in micro-USD, latency, scope

// Queue a repeatable comparison: the sample, expected answers, scoring and each cell's prompt are pinned now.
var batch = await evals.QueueAsync(new EvalMatrixRequest
{
    Set = "afisha-10", Models = ["anthropic/claude-haiku-5-5", "openai/gpt-6-luna"], Versions = [1, 2], Count = 10, Seed = 1,
});

// Forms 2 and 3 — the same brokers over the running Ai service; swap the registrations, keep the code.
services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.com/"))
    .AddHttpMessageHandler<HostTokenHandler>();   // the host's own handler: an Identity token with ai.* scopes

// A host serving the API itself: the module or the client behind it, its own authentication in front.
services.AddAiApi();
app.MapAiApi();   // api/ai/… — ai.read · ai.run · ai.prompts-manage · ai.evals-manage
```

## Layout

```
product/                 ← the capability: spec, state, features, flows, brand (no code)
engineering/             ← the how: architecture (contract, package registry), planning, deployment, scripts
└── codebase/
    ├── ai.backend-services/    ← packages/ (the NuGet family) · service/Ai.Api · tests/
    └── ai.frontend-services/   ← packages/fe-ai (npm) · apps/web (console)
```

## Run it (dev)

```bash
# Database: the compose file's local PostgreSQL (port in engineering/deployment/deployment.md); appsettings.Local.json
# (gitignored) points the service at it, read because the https launch profile sets LOCAL_SETTINGS=true. A local run
# applies pending migrations at boot.
docker compose -f engineering/deployment/docker-compose.yml up -d database

# Service
cd engineering/codebase/ai.backend-services && dotnet run --project service/Ai.Api --launch-profile https

# Pages and console, proxied to the service
cd engineering/codebase/ai.frontend-services && pnpm install --frozen-lockfile && pnpm dev
```

## Test it

```bash
# Backend: unit, providers, integration, E2E, conformance and migrations tiers; all but unit and providers run PostgreSQL in Docker
# (Testcontainers), and the build also builds the frontend workspace, so Node and pnpm are required
cd engineering/codebase/ai.backend-services && dotnet test Ai.BackendServices.slnx

# Frontend: lint, tests (builds fe-ai first), production build, packed-consumer check
cd engineering/codebase/ai.frontend-services && pnpm lint && pnpm test && pnpm build && pnpm check:package

# Release scripts: the tag version logic
bash engineering/scripts/test-next-version.sh
```

## Pointers

- Capability spec: `product/product.md` · state and decisions: `product/context.md`
- Contract: `engineering/architecture/contract.md` · packages: `engineering/architecture/package-registry.md`
- Releases: `engineering/scripts/scripts.md` · deployment: `engineering/deployment/deployment.md`
- Plan: `engineering/planning/backlog.md` · active version: newest `engineering/planning/version-track/v{X.Y}/`
- Conventions: `wow-two-ws/conventions/development/repo/structure/domain-structure.md`
