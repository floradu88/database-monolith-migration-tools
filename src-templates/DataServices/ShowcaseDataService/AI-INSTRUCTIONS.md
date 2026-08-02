# AI Instructions — ShowcaseDataService

Golden DB-as-a-Service template. Prefer this over CustomerDataService when scaffolding new domains.

## Mandatory

1. Preserve hybrid ownership: SQL project owns SPs/views/security; EF owns selected tables only — never dual-own.
2. Do not invent credentials, production connection strings, or platform approvals.
3. AMBIGUOUS DbIntelligence findings are not ownership — keep on review queue.
4. SourceFacade may call monolith SPs; Owned uses target DB; Shadow compares reads only (no dual-write).
5. When behavior changes, update docs, manifests, tests, RBAC notes, observability, and rollback together.
6. Never auto-execute destructive SQL.

## Completion report

Changed files; assumptions; validation; risks; required human approvals.
