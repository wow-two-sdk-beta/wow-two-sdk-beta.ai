# WoW2.Sdk.Beta.Be.Ai.Client

Forms 2 and 3 of the Ai capability: `IAiRunner`, `IPromptsBroker`, `IModelPricesBroker`, `IInferencesBroker` and
`IEvalsBroker` over the running Ai service's HTTP API (the Hosting package's routes under `api/ai/`), sharing one named
`HttpClient`. A product on Form 1 switches by swapping registrations — `AddAiClient` in place of `AddAi()` and
`AddAiProviders` — and keeps the code that calls the brokers.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Client --prerelease
```

```csharp
services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.com/"))
    .AddHttpMessageHandler<HostTokenHandler>();   // the host's own handler: an Identity token with ai.* scopes
```

- The service checks an `ai.*` scope per route and grants each caller its data scopes; a call naming no scope lands in
  the caller's default one, and a set, item or run outside the caller's scopes reads as not found.
- The service's problem details come back as the contract's errors (`code` names the `AppErrorType`); an unreachable
  service is `ExternalUnavailable`.
- `IEvalsBroker.RunAsync` waits for the whole run on one request: queue a large comparison (`QueueAsync`) and read its
  status instead, or raise the timeout through the returned builder.
