# development/

Repository-specific development guidance:

- [Frontend](frontend-guidelines.md): pnpm workspace, Vue app, verification, and host-copy commands.
- [Backend](backend-guidelines.md): .NET guidance.
- [Iterations](iteration-guide.md): planning and acceptance workflow.
- [Rules](rules.md): working rules for agents in this repo.
- [Repository](#repository): how the repo was created, its GitHub settings and its rollout state.

## Repository

- Created by the hub's `create-library` skill (phase 1), never by hand; `origin` comes from `gh repo clone`.
- GitHub settings, per wow-two-ws/conventions/development/repo/structure/domain-structure.md § Repository:
  - public in `wow-two-sdk-beta`;
  - issues, wiki, projects and discussions off;
  - workflow token read-only by default and unable to approve pull requests; a workflow raises permissions per job;
  - `main` under the `main-guard` ruleset (wow-two-ws/scripts/release/main-guard.json): no deletion, no force push,
    no review rule.
- Rollout state: from the hub root run `python3 -I scripts/release/library.py plan wow-two-sdk-beta.ai --brand Ai`.
  - It probes every phase (GitHub, scaffold, hub, registries, image, Wheelhouse, consumers) and prints the next
    commands; the `status` subcommand in place of `plan` prints the table only.
  - No probe sees the NuGet policy, Wheelhouse or secrets; once they are checked, add
    `--confirmed nuget-policy,wheelhouse,secrets`.
  - The procedure: wow-two-ws/.claude/skills/create-library/SKILL.md.
- Hand steps left per library, each the developer's:
  - NuGet trusted-publishing policy for `publish-packages.yml`, packages `WoW2.Sdk.Beta.Be.Ai.*` → unblocks
    arming `PUBLISH_PACKAGES` and the be publish;
  - npm first version of `@wow-two-beta/fe-ai`, then `npm trust` for `publish-packages.yml` (2FA) → unblocks the
    fe job;
  - Wheelhouse product registration: import, server, sites → dev rollouts of the image;
  - third-party secret values, entered in Wheelhouse → the dev verify check;
  - public DNS for each public site → the public site; prod promotion stays the owner's.

Rules: wow-two-ws/conventions/development/
