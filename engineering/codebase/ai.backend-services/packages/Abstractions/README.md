# WoW2.Sdk.Beta.Be.Ai.Abstractions

The Ai contract: `IGreetingsBroker`, `GreetingModel`, `GreetingConstants`, `GreetingRouteConstants` and the
`GreetingErrors` catalogue. A product's Domain and Application projects reference this package only; its composition
root picks an implementation:

- `WoW2.Sdk.Beta.Be.Ai.Module` — Form 1, in process, over the product's own database.
- `WoW2.Sdk.Beta.Be.Ai.Client` — Forms 2 and 3, over the running Ai service.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Abstractions --prerelease
```
