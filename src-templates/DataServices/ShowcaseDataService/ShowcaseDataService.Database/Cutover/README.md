# Cutover scripts (up / down)

Ordered, reversible waves for **FacadeThenMove**. These are **not** SSDT Build objects.

## Rules

- Desired-state SP/schema definitions live under `Programmability/`, `Security/`, `Contract/` and publish via dacpac.
- `*.up.sql` / `*.down.sql` here cover compatibility wrappers, grants toggles, and rollback steps around cutover.
- Never auto-execute against production; DBA review required.
- Prefer additive / permission-based soft blocks over dropping bodies in place (see `docs/06-stored-procedure-function-retirement.md`).

## Apply order (owned path)

1. PreDeploy
2. EF migrations (`ShowcaseDataService.Migrations`) for EF-owned tables
3. SQL project dacpac (SPs + contract)
4. Approved `NNN_*.up.sql` in lexical order
5. PostDeploy (RBAC + contract stamp)

Rollback: apply matching `*.down.sql` in reverse order, then redeploy previous dacpac / switch route to SourceFacade.

## Naming

- `{ordinal}_{slug}.up.sql` / matching `*.down.sql` — keep pairs in sync.
- `003_register_workitem_pair` registers the dbo/core ShowcaseWorkItem DualWritePair (delta-only; down disables the pair, does not DROP core).
