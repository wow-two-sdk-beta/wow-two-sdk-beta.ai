# WoW2.Sdk.Beta.Be.Ai.Client

Forms 2 and 3 of the Ai capability: the contract over the running Ai service's HTTP API. Today it registers the
client's options and its named `HttpClient` only; the HTTP brokers (`IAiRunner`, `IPromptsBroker`, `IEvalsBroker`, …)
land with the service's controllers (backlog). Until then a product hosts the module in process with `AddAi()`.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Client --prerelease
```

```csharp
services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.com/"));
```
