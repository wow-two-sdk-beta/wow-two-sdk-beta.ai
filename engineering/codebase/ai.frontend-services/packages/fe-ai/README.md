# @wow-two-beta/fe-ai

The Ai domain's UI, in two modes, plus the management screens a console composes:

- **Embed** — `GreetingWidget` greets through the Ai contract. It calls the consumer's own backend (Form 1, the
  default same-origin client) or the running Ai service (Forms 2 and 3, `createGreetingsClient({ baseUrl })`).
- **Forward** — `forwardToHostedGreeting` sends the user to the service's hosted greeting page, which links back to
  `returnUrl` when done.
- **Admin** — `GreetingsStatusPanel` probes the contract through the same client and shows whether it answers. The
  service's own console composes it; a central console or a product's admin page imports the same export.

```bash
pnpm add @wow-two-beta/fe-ai @wow-two-beta/fe vue
```

```ts
import { GreetingWidget, createGreetingsClient, forwardToHostedGreeting } from '@wow-two-beta/fe-ai/kit';
import { GreetingRouteConstants, type GreetingModel } from '@wow-two-beta/fe-ai/contracts';
import { GreetingsStatusPanel } from '@wow-two-beta/fe-ai/admin';
```

The widget and the panels are styled with Tailwind utilities on the fe sdk theme; add the package to the consumer's Tailwind sources:

```css
@source '../../node_modules/@wow-two-beta/fe-ai/dist';
```

| Export | Holds |
|---|---|
| `./contracts` | `GreetingModel`, `GreetingConstants`, `GreetingRouteConstants` — the TypeScript mirror of the be contract |
| `./kit` | `GreetingWidget`, `createGreetingsClient`, `hostedGreetingUrl`, `forwardToHostedGreeting` |
| `./admin` | `GreetingsStatusPanel` — the management screens; each takes an optional `client`, like the widget |
