# 10. Architecture Decisions and Risks

## Decisions

### Logical isolation before physical split

Schema ownership and access control expose hidden coupling with lower operational risk than immediately moving databases.

### Intelligence platform separate from monolith

The catalog must observe many source and target databases and survive the monolith's retirement.

### Migration control plane separate from business services

Migration orchestration is transitional; domain data services are permanent.

### Static plus runtime evidence

DMVs are cache-based, Query Store is query-oriented, Extended Events can be incomplete or filtered, and source scanning misses external callers. Combined evidence is mandatory.

## Major risks

| Risk | Mitigation |
|---|---|
| Hidden callers | Dedicated identities, XE/Audit, long observation, jobs/reports/ETL inventory |
| Telemetry overhead | Lightweight profile, filters, event-file target, load testing, monitoring |
| Wrong ownership | Write-based scoring, business review, explicit approval |
| Dynamic SQL | ScriptDom plus heuristics, runtime query text, targeted tracing |
| Distributed transactions | Saga, outbox, idempotency, compensation |
| Dual-write drift | Prefer outbox/CDC, continuous reconciliation, replay |
| Chatty service calls | Coarse contracts, batch APIs, read models, caching |
| AI hallucination | Evidence links, structured output, confidence, human gates |
| Seasonal false negatives | 6–12 month retention where required |
| Rollback gaps | immutable source artifact, reverse scripts, rehearsed cutover |
