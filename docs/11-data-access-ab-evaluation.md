# Data Access A/B Evaluation: EF Core, Dapper, and Hybrid

## Recommended default

Use a hybrid:

- EF Core for aggregate persistence and ordinary CRUD;
- Dapper for tuned reads and explicit stored-procedure contracts;
- stored procedures for set-based work and migration façades;
- ADO.NET only for low-level infrastructure needs.

## Safe A/B methods

Prefer:

- production-like replay;
- shadow reads;
- mirrored read traffic;
- canary instances;
- feature flags.

Do not randomly A/B non-idempotent writes.

## Metrics

Measure:

- p50/p95/p99 latency;
- throughput;
- application and SQL CPU;
- logical reads/writes;
- allocations;
- pool usage;
- lock duration;
- timeouts/errors;
- generated SQL stability;
- plan count;
- maintenance complexity.

## Decision matrix

| Workload | Default |
|---|---|
| CRUD | EF Core |
| Projection read | EF Core no-tracking, then benchmark |
| Complex tuned read | Dapper |
| Existing critical procedure | Dapper/ADO.NET façade |
| Bulk operation | Stored procedure/bulk API |
| Cross-shard read | Dedicated query/read-model service |

## Shadow-read sequence

1. Execute authoritative implementation.
2. Return its result.
3. Execute candidate asynchronously.
4. Normalize and compare.
5. Record mismatch and performance.
6. Promote only after correctness and SLO gates pass.
