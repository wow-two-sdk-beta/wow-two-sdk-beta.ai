# wow-two-sdk-beta.ai

WoW2 AI: routes model calls across providers with fallback, keeps prompts and their versions in tables, logs every inference with its tokens and cost, and runs evals that compare prompts and models on one report.

## Artifacts

| Artifact | Registry | Line · tag | Install |
|---|---|---|---|
| `WoW2.Sdk.Beta.Be.Ai.Abstractions` — the contract | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Abstractions --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Providers` — named model providers and the one-attempt caller | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Providers --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Module` — Form 1, in process | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Module --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Client` — Forms 2 and 3, over HTTP | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Client --prerelease` |
| `WoW2.Sdk.Beta.Be.Ai.Testing` — scripted model caller and conformance suite | nuget.org | `10.y.z-beta` · `be-v*` | `dotnet add package WoW2.Sdk.Beta.Be.Ai.Testing --prerelease` |
| `@wow-two-beta/fe-ai` — contract mirror, formatters, eval report view | npm | `0.0.z` · `fe-v*` | `pnpm add @wow-two-beta/fe-ai` |
| `ai` service image | GHCR | `X.Y.Z` · `v*` | `ghcr.io/wow-two-sdk-beta/wow-two-sdk-beta.ai/ai`, deployed by Wheelhouse |

A product pins the family with one `AiSdkVersion` property beside `BackendSdkVersion`.

## Forms

```csharp
// Form 1 — in process, over the product's own PostgreSQL database; its migrate verbs apply the module's set.
// Providers come from Ai:Providers:{name} (Kind, BaseUrl, ApiKey, TimeoutSeconds); keys from user-secrets or a target secret.
services.AddAiProviders(configuration);
services.AddAi().Configure(options => options.ConnectionString = "...");

// Then any product code runs a use case; its prompt lives in the ai_prompts table, its route in ai_use_cases.
var run = await runner.RunAsync(new AiRunRequest { UseCase = "ocharo-relevance", Variables = variables, Subject = subject });
// run.Outcome, run.Output (JSON), run.Attempts: model, tokens, cost in micro-USD, latency

// Forms 2 and 3 — over the running Ai service; brokers land with the HTTP API (backlog)
services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.com/"));
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
# Backend: unit, integration, E2E, conformance and migrations tiers; all but unit run PostgreSQL in Docker
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
