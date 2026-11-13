# WoW2.Sdk.Beta.Be.Ai.Client

Forms 2 and 3 of the Ai capability: `IGreetingsBroker` over the running Ai service's HTTP API. The product
changes form by configuration: swap `AddAi()` for `AddAiClient(…)` in its composition root; no other code
changes.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Client --prerelease
```

```csharp
services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.com/"));
```
