# AI Instructions — ShowcaseDataService

Golden DB-as-a-Service template. Prefer this over CustomerDataService when scaffolding new domains.

## Mandatory

1. Preserve hybrid ownership: SQL project owns SPs/views/security/contract; EF owns selected tables only — never dual-own (`object-ownership.yml`).
2. Migrated SP definitions live as desired-state Build scripts under `ShowcaseDataService.Database/Programmability/`; cutover up/down stays in `Cutover/` (not Build).
3. Configure schema + connections only under `Database` in appsettings (or `Database__*` env). Do not hardcode `dbo`/`showcase` or connection strings in call sites.
4. Set `Owned:Provider` / `SourceFacade:Provider` to `OnPrem`, `Azure`, or `Aws` and use `DATABASE-HOSTING.md` patterns; do not invent cloud resources or credentials.
5. For `$"{a}_{b}"` / `{ValueA}_{ValueB}` procedure names, map holes to enums or constants (`ShowcaseProcedureNames` / `StoredProcedureName`) so discovery can expand concrete SPs.
6. AMBIGUOUS DbIntelligence findings are not ownership — keep on review queue.
7. SourceFacade may call monolith SPs; Owned uses target DB; Shadow compares reads only (no dual-write). `ParallelWrite` fans out dbo + core SPs on writes (dbo is caller result; core failure is evidence).
8. When behavior changes, update docs, manifests, tests, RBAC notes, observability, and rollback together.
9. Never auto-execute destructive SQL.
10. When changing `Database:Schema`, update matching SQL project scripts in the same change (SSDT is not bound to appsettings).

## Completion report

Changed files; assumptions; validation; risks; required human approvals.
