# ShowcaseDataService.Database Scripts

Human-gated PreDeploy / PostDeploy stubs for the golden SQL project. **Never auto-apply to production.**

| Script | Role |
|--------|------|
| [`PreDeploy.sql`](PreDeploy.sql) | Idempotent schema guards (`showcase`, `deployment`) before EF / dacpac |
| [`PostDeploy.sql`](PostDeploy.sql) | Contract stamp + RBAC grant **placeholders** (commented; no invented principals) |

## Deploy order (reviewed)

Documented in the parent [`../README.md`](../README.md):

1. PreDeploy  
2. EF migrations (EF-owned tables only)  
3. SQL project dacpac  
4. Approved `../Cutover/*.up.sql`  
5. PostDeploy  

## Apply policy

- Local lab / DBA-reviewed environments only.
- Do not invent connection strings or credentials in this folder.
- Cutover up/down scripts stay under `../Cutover/` (not Build) — see [`../Cutover/README.md`](../Cutover/README.md).
- Align real grants with kit `sql/common/21-create-rbac-roles.sql` after human review.

## Related

- Ownership: [`../object-ownership.yml`](../object-ownership.yml)
- Parent database README: [`../README.md`](../README.md)
- Auth (API JWT is separate): [`../../AUTH.md`](../../AUTH.md)
