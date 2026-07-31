# Production Decomposition Runbook

1. Discover objects, callers, identities, dependencies, and performance.
2. Assign one owner and target service.
3. Encapsulate existing procedures behind a stable service.
4. Benchmark EF Core/Dapper/hybrid per operation.
5. Deploy target database with RBAC, Query Store, Audit, backup, and alerts.
6. Backfill and synchronize.
7. Shadow reads and reconcile.
8. Canary controlled traffic.
9. Cut over with rollback route.
10. Revoke legacy access, watch calls, then decommission.

## Cutover gates

- known callers identified;
- unknown-call threshold approved;
- SLOs pass;
- capacity test passes;
- reconciliation passes;
- rollback rehearsed;
- RBAC approved;
- monitoring live;
- restore tested;
- no unmanaged drift.
