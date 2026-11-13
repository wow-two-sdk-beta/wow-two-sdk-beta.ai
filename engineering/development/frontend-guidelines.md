# wow-two-sdk-beta.ai — Frontend Development Guidelines

Defer to `wow-two-ws/conventions/development/frontend/`.

## Workspace

- Frontend root: `engineering/codebase/ai.frontend-services/`; Node `24.11.0` from `.nvmrc`, `pnpm@10.33.2`.
- `packages/fe-ai` is `@wow-two-beta/fe-ai`, published to npm: `./contracts` (the TypeScript mirror of the be
  contract), `./kit` (`GreetingWidget`, `createGreetingsClient`, `forwardToHostedGreeting`) and `./admin`
  (`GreetingsStatusPanel`, the management screens). It peers `@wow-two-beta/fe` at `>=0.0.n <0.1.0`, `n` the pinned
  version; `check:package` enforces it and fails when one of the three exports is missing.
- `apps/web` is `@ai/web`, private: the hosted pages (`/greet`) and the console (`/`). It takes `fe-ai` by
  `workspace:*`, through its `exports` only — the way a product would; the package never imports `apps/`.
- App source uses `bootstrap`, `presentation`, `application`, `domain`, and `integration` layers.
- Routes live in `src/bootstrap/router/Routes.ts` on the fe sdk router (`createAppRouter`, `lazyPage`); a new hosted
  page is one route there, its path a constant in `fe-ai/contracts`, its props decoded from the route.
- The kit's UI is form-agnostic: it calls the consumer's origin (Form 1) or the service's (`baseUrl`), never both.
- Management screens live in `fe-ai/src/admin/`, exported as `./admin`, each taking an optional client like the
  widget; the console page composes them, and a central console or a product's admin page imports the same export.
  The console never builds a management screen of its own.

## Commands

Run from the frontend root; `dev`, `typecheck` and `test` build `fe-ai` first, since `apps/web` reads its `dist/`.

```sh
pnpm install --frozen-lockfile
pnpm dev            # https on 8396, /api proxied to the service's https profile on 8420
pnpm lint && pnpm test && pnpm build && pnpm check:package
```

`pnpm dev:http` or `VITE_HTTPS=false pnpm dev` skips local certificate setup; `VITE_API_TARGET` picks another backend.

## Delivery

`pnpm build` builds `fe-ai`, then `apps/web/dist/`; `pnpm deploy` copies it into the service's `wwwroot/`, and a
backend build runs it too (`BuildSpa`). `publish-packages.yml` publishes `fe-ai` alone, at the version its
`fe-v*` tags and npm give.
