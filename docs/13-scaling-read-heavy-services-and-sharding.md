# Scaling Smaller Databases and DAL Microservices

## Scale in this order

```text
Query/index optimization
→ connection-pool control
→ cache
→ read models
→ read-only replicas
→ vertical scaling
→ elastic pools
→ partitioning
→ sharding
```

## Read routing

| Read | Path |
|---|---|
| Latest state required | Primary |
| Replica lag acceptable | Read replica |
| Repeated key lookup | Cache |
| Cross-domain page | Materialized read model |
| Search | Search index |
| BI/reporting | Reporting store |

## Stateless DAL service

Scale horizontally with:

- bounded pools;
- concurrency limits;
- rate limits;
- backpressure;
- timeouts/cancellation;
- cache-stampede protection;
- idempotency;
- transient-only retries;
- circuit breakers.

## Connection budget

```text
max instances × pool size
+ migration/operations connections
+ monitoring connections
< safe database session/worker budget
```

Auto-scaling must consider database saturation, not only service CPU.

## Sharding conditions

Shard when a single database cannot meet throughput/capacity economically, isolation or residency demands it, restore windows are excessive, and a stable shard key exists.

A good shard key is stable, evenly distributed, present in most operations, and minimizes cross-shard transactions.

## Cross-shard rules

- no distributed transaction by default;
- route writes to one shard;
- use sagas;
- use globally unique IDs;
- build global reporting through events/ETL;
- fan-out only in a dedicated bounded query service.
