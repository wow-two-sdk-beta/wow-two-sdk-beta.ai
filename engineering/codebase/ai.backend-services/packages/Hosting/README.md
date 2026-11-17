# WoW2.Sdk.Beta.Be.Ai.Hosting

The Ai HTTP API any ASP.NET Core host mounts over the contract's brokers — the module in process (`AddAi()` beside
`AddAiProviders(configuration)`), or any other implementation. `AddAiApi()` registers one authorization policy per
`ai.*` scope and the data-scope seam; `MapAiApi()` maps the routes under `api/ai/`.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Hosting --prerelease
```

```csharp
builder.Services.AddAiProviders(builder.Configuration);
builder.Services.AddAi().Configure(options => options.ConnectionString = "Host=…;Database=…");
builder.Services.AddAiEvalWorker();                       // optional: runs queued evals in this host
builder.Services.AddAiApi().BindConfiguration("Ai:Api");  // policies + IAiScopeAccess
// the host's own authentication (Identity tokens), then:
app.UseAuthentication();
app.UseAuthorization();
app.MapAiApi();
```

- Scopes: `ai.read` (catalog, inferences, totals, sets, runs, reports), `ai.run` (`POST runs`, subject purges),
  `ai.prompts-manage` (use cases, prompts, labels, prices), `ai.evals-manage` (sets, items and their inputs, queue,
  cancel, ratings, export, import). No token answers 401, a token without the route's scope 403. The handler reads the
  `scope`/`scp` claims; a host with another principal adds its own handler for `AiScopeRequirement`.
- `Ai:Api:AllowAnonymousInDevelopment` lifts the scopes in the Development environment only; elsewhere the host fails to
  start.
- Data scopes: register an `IAiScopeAccess` that maps the caller to its `tenant:{id}` scopes (`AiScopeGrant.Of`), or
  `AiScopeGrant.Every` for operators. The default grants the unscoped bucket only. Lists keep the caller's scopes; a set,
  item or run outside them answers 404; a write naming another scope answers 403. Use cases and prompts are shared.
- Bodies: success under `data` on the be sdk's JSON preset; failures as the be sdk's problem details (`code` = error
  type). `POST evals/sets/{key}/runs` answers `202` with the batch and its run ids; `GET evals/sets/{key}/report`
  takes `?runs=&answers=&format=json|csv|md`.
