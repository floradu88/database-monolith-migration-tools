# Claude Code — SQL DB Modernization Kit

Use this repository as a production-grade kit for decomposing a SQL Server monolith into independently owned data services and databases.

## Before any change

1. Read `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and `docs/CANONICAL-DOCUMENT-INDEX.md`.
2. Read the nearest `AI-INSTRUCTIONS.md` for the folder you will edit (see `AI-INSTRUCTION-INDEX.md`).
3. Prefer canonical docs listed in the root README “Start here” section over supplemental/legacy docs.


## Safety rules

- Preserve ownership boundaries; prefer additive, reversible changes.
- Do not invent credentials, approvals, platform capabilities, or production values.
- Never execute destructive SQL automatically.
- Do not apply SQL scripts to production without DBA review.
- Keep SQL project ownership and EF Core migrations ownership non-overlapping.
- When behavior changes, update docs, manifests, tests, RBAC, observability, and rollback guidance together.

## Kit layout

| Path | Role |
|------|------|
| `docs/` | Architecture, migration plan, runbooks (canonical + supplemental) |
| `sql/` | Discovery, telemetry, audit, RBAC, and platform SQL scripts |
| `manifests/` | Domain ownership and migration-wave examples |
| `src-templates/` | .NET solution/project scaffolds (source split, data services, control plane) |
| `src-templates/DbIntelligence/scripts/` | PowerShell setup/run; prefer `Invoke-DbIntelligenceReady.ps1` (path only); fnm Node + Codegraph via `fnm exec --using=lts-latest` |
| `src-templates/CodegraphChat/scripts/` | Topic chat Ready: `Invoke-CodegraphChatReady.ps1` (path only) → http://localhost:5091/ |
| `checklists/` | Cutover and source-split checklists |
| `validation/` | Checksums and validation summary |
| `HOW-TO-USE.md` | Canonical PowerShell command reference (DbIntelligence + CodegraphChat Ready) |

## Completion report

When finishing work, report:

- changed files;
- assumptions;
- validation performed;
- unresolved risks;
- required human approvals.
