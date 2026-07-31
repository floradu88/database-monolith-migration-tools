# DML and Programmability-Object Access Tracking

## Initial scope

Track execution/access, not row values.

### Operations

- SELECT when needed;
- INSERT;
- UPDATE;
- DELETE;
- MERGE;
- EXECUTE;
- procedure/function/view/trigger access;
- dynamic SQL entry points.

## Mechanisms

| Question | Mechanism |
|---|---|
| Who executed a procedure? | SQL Audit + RPC XE + identity/session context |
| What ran and how expensive? | Query Store |
| Was DML issued? | SQL Audit INSERT/UPDATE/DELETE/SELECT |
| What did a procedure touch? | Static parsing + plans/text + targeted statement XE |
| Was a function used? | Static dependencies + Query Store + function DMVs |

Inline TVFs and inlined scalar UDFs may not appear as independent runtime executions.

## Volume rollout

1. EXECUTE on target schemas.
2. INSERT/UPDATE/DELETE.
3. SELECT only where required.
4. Statement-level XE only for targeted windows.
5. Aggregate events hourly.
6. Retain raw audit files per policy.
