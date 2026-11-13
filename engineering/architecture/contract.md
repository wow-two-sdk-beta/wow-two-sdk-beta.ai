# wow-two-sdk-beta.ai — Contract

*Last updated: 2026-11-13*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract.

## Greetings

| Member | Returns | Failures |
|---|---|---|
| `IGreetingsBroker.GreetAsync(name)` | `Result<GreetingModel>`: `Hello, {trimmed name}!`, or `Hello, world!` for a blank name, stamped `GreetedAtUtc` | `Validation` when `name` exceeds `GreetingConstants.MaxNameLength` (80) |

| Form | Implementation | Wire |
|---|---|---|
| 1 | `ModuleGreetingsBroker` (Module) | in process; stores nothing (the template ships no table) |
| 2/3 | `ApiGreetingsBroker` (Client) | `GET api/greetings?name=` → `{ data: GreetingModel }`; 400 problem → `Validation`; other failures → `ExternalUnavailable` |
| fe | `createGreetingsClient` (`fe-ai/kit`) | the same `GET`, from the consumer's origin or the service's |

- Conformance: `GreetingsBrokerConformance` (`packages/Testing`) runs against the module, the client (in-process host
  or `CONFORMANCE_BASE_URL`), and the fake — `tests/Ai.Tests.Conformance`.
- Hosted page: `/greet?name=&return_url=` — the forward target of `forwardToHostedGreeting`; it links back to an
  absolute http(s) `return_url` only.
