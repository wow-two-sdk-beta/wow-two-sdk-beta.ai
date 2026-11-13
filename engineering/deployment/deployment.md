# deployment/

How wow-two-sdk-beta.ai's service ships. Descriptor format: wow-two-ws/conventions/deployment/descriptor/deploy-descriptor.md.
The packages ship separately, from `publish-packages.yml` (`engineering/scripts/scripts.md`).

| File | Role |
|---|---|
| `deploy.yml` | descriptor 2: one service, `ai` (`/health`, 256 MB, the typed required key `DatabaseSettings:ConnectionString`, `needs: [postgres]`); sites `app` (public, primary) and `console` (private), both at `/` and `/api`; `dotnetEnvironment: true`, the `verify` dev check and the `migrate:` step |
| `Dockerfile` | three stages: build the frontend workspace (`fe-ai`, then `apps/web`), publish `service/Ai.Api` with its packages and the pages in `wwwroot`, run on `aspnet` with `curl` |
| `docker-compose.yml` | local verification: PostgreSQL (loopback `8422`), the one-shot `ai-migrate` running the image's `migrate`, then the service on `http://127.0.0.1:8421` |

## Contract with Wheelhouse

- Image: `ghcr.io/<owner>/<repo>/ai`, published by `.github/workflows/publish-docker-image.yml` through the shared
  `delivery.yml` of `wow-two-platform.pipelines` (pinned `v0.12.0`); CI never deploys. `v0.10.0` and later carry the
  typed setting keys (`settings.keys`) into the bundle, and `v0.12.0` the `migrate:` step and the sets each item's
  image hosts; the runner needs Wheelhouse v0.5 or later.
- Version: `X.Y` from the newest `planning/version-track/v{X.Y}/`; the image tag and `GET /api/system/status` report the
  same `X.Y.Z`, plus the build's commit after a `+`. Package releases (`be-v*`, `fe-v*`) never move it.
- Settings a target provides in `/app/appsettings.Target.json`: `DatabaseSettings:ConnectionString`, which Wheelhouse
  fills from the target's PostgreSQL database; `Deployment:TrustedProxies` or `Deployment:TrustedNetworks` names the
  ingress.
- Database: PostgreSQL, the target's own (`needs: [postgres]`); the module's set lives in schema `ai`. `GET /health`
  answers 200 only while the database answers.
- Migrations: the image answers `dotnet Ai.Api.dll migrate <verb>`, with exits `0` done · `1` refused · `2` failed ·
  `3` busy and a last `migrate-result` line. A deploy runs the bare `migrate` (apply, then seed) from the release image
  before the first service restarts, as `deploy.yml`'s `migrate:` item declares (pipelines v0.12.0); a deployed boot
  only verifies the levels and refuses to serve a database behind it. `migrate sets --json` needs no database.
- Dev check: after a dev rollout, `verify` curls `$VERIFY_SERVICE_AI/health` and `/api/system/status` with
  `Host: localhost`; `HostFilteringTests` replays the descriptor's requests.
- After each main image publish, `conformance.yml` boots the released image, reads its status and runs `ai usecase list`
  over the migrated database.

## Local run

- An IDE run reads `service/Ai.Api/appsettings.Local.json` (gitignored; the `https` launch profile sets
  `LOCAL_SETTINGS=true`) for its connection string. `docker compose up -d database` (from this folder) starts the local
  PostgreSQL on loopback `8422`:

```json
{ "DatabaseSettings": { "ConnectionString": "Host=127.0.0.1;Port=8422;Database=ai;Username=ai;Password=ai" } }
```

- A local run (`Development` without `DEPLOY_ENVIRONMENT`) creates the database if missing and applies pending
  migrations at boot; `dotnet run --project service/Ai.Api -- migrate check` reports the levels without writing.
- Local image check:

```bash
docker build --build-arg APP_VERSION=0.1.99 --build-arg VCS_REF=$(git rev-parse HEAD) \
  -f engineering/deployment/Dockerfile -t ai:local engineering/codebase
```
