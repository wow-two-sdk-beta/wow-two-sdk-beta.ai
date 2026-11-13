# wow-two-sdk-beta.ai — Backend Development Guidelines

Defer to wow-two-ws/conventions/development/backend/ and the domain repo shape in
wow-two-ws/conventions/development/repo/structure/domain-structure.md. Repo deltas:

- `packages/*` are the NuGet family: packable, XML docs, warnings as errors, a `README.md` beside each csproj (all set
  by path in `Directory.Build.props`). `service/` and `tests/` are not packable.
- A new package is a `packages/{Layer}/WoW.Two.Sdk.Beta.Be.Ai.{Layer}.csproj`; its id drops `.Two` (`WoW2.…`), its
  namespaces root at `WoW.Two.Sdk.Beta.Be.Ai`, and its row joins `engineering/architecture/package-registry.md`.
- The contract (`Abstractions`) holds no DI registration, options binding or I/O; a new broker member gets a
  conformance case in `packages/Testing` that every implementation passes.
- Store: PostgreSQL through `DatabaseSettings:ConnectionString` (`DB_CONNECTION` wins). The module owns the named set
  and schema `ai` (`AiModuleConstants`), claimed as `module` in a Form 1 product and as `service` by
  `Ai.Api`. Tables live in that schema and carry the `ai_` prefix. A new table is a new
  `packages/Module/Migrations/NNN-name/{Apply,Rollback}.sql` pair, starting at `002`; never edit an applied one. Each
  file is idempotent and expands, or carries `-- @contract since=vX.Y`; the migrations tier lints and proves both.
- `dotnet Ai.Api.dll migrate` is the deploy's step; `check`, `plan` and `sets --json` write nothing.
- `dotnet test Ai.BackendServices.slnx` runs every tier; the backend build needs Node and pnpm for the pages, and
  every tier but unit needs Docker.
