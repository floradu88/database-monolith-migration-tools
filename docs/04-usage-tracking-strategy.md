# 4. SQL Server Usage Tracking Strategy

## Why several mechanisms are required

- `sys.dm_exec_procedure_stats` and `sys.dm_exec_function_stats` are cache-based and reset when plans are evicted, recompiled, or the engine fails over/restarts.
- Query Store persists query/runtime history but is query/plan oriented and depends on capture and retention settings.
- Extended Events provides caller and execution evidence but must be filtered and sized carefully.
- SQL Audit is appropriate for security/compliance evidence but is not a full performance telemetry system.
- static parsing catches references that have not executed during the observation period.

## Mandatory application attribution

Every application receives:

1. a distinct SQL login or managed identity;
2. a unique connection-string `Application Name`;
3. session context values such as application, version, environment, and correlation ID.

Attribution priority:

```text
Dedicated identity > trusted SESSION_CONTEXT > client_app_name
> host/resource mapping > query-signature inference > unknown
```

## Tracking layers

### Layer 1: object inventory

Regularly snapshot all SQL modules, definitions/hashes, parameters, dependencies, jobs, synonyms, users, roles, and permissions.

### Layer 2: lightweight runtime tracking

- Query Store enabled and retained;
- scheduled DMV snapshots for procedure/function/trigger statistics;
- lightweight Extended Events for RPC and batch completion;
- application identity and session attribution.

### Layer 3: targeted deep tracing

Use statement-level or module-level events only for selected applications/objects and limited windows. Avoid permanent unfiltered statement capture.

### Layer 4: security audit

Audit privileged actions, sensitive object access, schema changes, and execution where compliance requires immutable evidence.

## Function-specific limitations

- Scalar function counters can be affected by configuration and scalar UDF inlining.
- Inline table-valued functions are expanded into caller plans and may not appear as independent runtime executions.
- A missing row in `sys.dm_exec_function_stats` does not prove non-use.

Function retirement therefore requires static dependency parsing, caller usage, Query Store/plan evidence, Extended Events where useful, and owner confirmation.

## Retention

- minimum initial observation: 30 days;
- normal recommendation: 90 days;
- seasonal, financial, quarterly, or annual workloads: 6–12 months;
- telemetry tables should aggregate hourly/daily and archive raw event files according to data classification and cost limits.

## Performance safeguards

- deploy lightweight events first;
- filter by database and exclude known monitoring noise;
- avoid collecting parameter values by default;
- use asynchronous event-file targets for production where supported;
- monitor dropped events, target size, storage cost, collector lag, and Query Store state;
- test overhead under production-like load before enabling deep tracing.
