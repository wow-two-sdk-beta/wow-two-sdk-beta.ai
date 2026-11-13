# WoW2.Sdk.Beta.Be.Ai.Module

Form 1 of the Ai capability: `IGreetingsBroker` in the consumer's process, over the consumer's PostgreSQL database.
The module owns the schema `ai`: `AddAi()` registers its embedded `Migrations/NNN-name/{Apply,Rollback}.sql` as
the named set `ai`, with its own history and lock, beside the consumer's own set. The consumer's `migrate` verbs
(the be sdk's `MigrateCommand`) apply it with every other set, and a local run's boot does.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Module --prerelease
```

```csharp
services.AddAi().Configure(options => options.ConnectionString = "Host=…;Database=…");
```
