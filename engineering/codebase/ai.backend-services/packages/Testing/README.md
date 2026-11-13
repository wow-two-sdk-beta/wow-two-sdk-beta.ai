# WoW2.Sdk.Beta.Be.Ai.Testing

The Ai test kit:

- `ScriptedAiModelCaller` — an `IAiModelCaller` that answers from a queue of scripted results (or a function) and
  records every request, so a consumer's tests run the real runner, its retries, fallback, schema check and cost with no
  provider. Register it in place of `AddAiProviders`.
- `PromptsBrokerConformance` — the prompt registry's suite: rewrite and versioned saves, labels and the resolution
  rules. Derive a test class, supply the broker, and every rule runs against that implementation.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Testing --prerelease
```

```csharp
var caller = new ScriptedAiModelCaller();
caller.Enqueue(ScriptedAiModelCaller.Answer("""{"relevance":"close"}""", inputTokens: 1200, outputTokens: 20));
services.AddSingleton<IAiModelCaller>(caller);
```
