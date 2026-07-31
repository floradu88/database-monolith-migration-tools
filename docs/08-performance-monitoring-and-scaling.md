# Performance, Monitoring, and Scalability

## Baseline before extraction

For every operation:

- invocation rate;
- p50/p95/p99 latency;
- SQL and app CPU;
- logical reads/writes;
- result size;
- pool wait time;
- blocking/deadlocks;
- errors/timeouts;
- plan count;
- peak concurrency.

## Observability planes

### Application

OpenTelemetry traces, metrics, logs.

### Database

Query Store, Audit, XE, capacity, waits, sessions, workers, storage.

### Migration

Backfill, lag, mismatch, canary, cutover, legacy calls.

## Service scaling

The DAL microservice is stateless and scales horizontally, with:

- bounded connection pools;
- operation concurrency limits;
- rate limits;
- retries only for transient failures;
- circuit breakers;
- cancellation;
- caching;
- backpressure.

## Connection budget

```text
instances × pool size
+ operations
+ migration
+ monitoring
< database safe capacity
```

## Read scaling

Use, in order:

1. query/index improvement;
2. local/distributed cache;
3. domain read model;
4. read-only replica;
5. reporting store;
6. search index.

## Many small databases

Use elastic pools when workloads are variable and peaks are not synchronized.

Monitor noisy neighbors and allow promotion to dedicated compute.

## Sharding

Shard only with a stable key and proven capacity need.

Components:

```text
Data Service
→ Shard Router
→ Shard Map
→ Connection Factory
→ Shards
```

Track tenant placement, hot shards, capacity, routing failures, and resharding progress.

Cross-shard transactions are prohibited by default.
