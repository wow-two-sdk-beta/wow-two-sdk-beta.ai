# WoW2.Sdk.Beta.Be.Ai.Module

Form 1 of the Ai capability, in the consumer's process over the consumer's PostgreSQL database:

- `IAiRunner` — resolves a use case's prompt for each routed model, renders its `{{name}}` placeholders, answers from a
  stored inference when the same prompt, model and input ran before, else calls the model with retries on transient
  failures, checks the answer against the output schema, falls back along the route, and logs every attempt with its
  tokens and cost.
- `IPromptsBroker` — use cases and their prompts in one table: rewrite mode updates a row in place, versioned mode
  inserts the next version and moves labels (`production`, `candidate`).
- `IModelPricesBroker`, `IInferencesBroker` — prices with history, and the inference log with totals.
- `IEvalsBroker` — eval sets, runs (one prompt version × one model), scores, ratings and the combined report.

The module owns the schema `ai`: `AddAi()` registers its embedded `Migrations/NNN-name/{Apply,Rollback}.sql` as the
named set `ai`, with its own history and lock, beside the consumer's own set. The consumer's `migrate` verbs (the be
sdk's `MigrateCommand`) apply it with every other set, and a local run's boot does. Models are called through the
Providers package; the host composes `AddAiProviders(configuration)` beside `AddAi()`.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Module --prerelease
```

```csharp
services.AddAiProviders(configuration);
services.AddAi().Configure(options => options.ConnectionString = "Host=…;Database=…");
```
