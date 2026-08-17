# dbo → core stored-procedure quality window

Use after the first source-project split (`docs/03-source-monolith-split.md` does **not** rename schemas). This window keeps `dbo` as the caller-facing writer and adds `core` clones in the **same database**.

Golden demo: [`src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](../src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md). Kit SQL: `sql/common/40`–`45`.

- [ ] Tables written by the SP are inventoried (`stored-procedure-map.json` / `sql/common/04`)
- [ ] Business key declared (not IDENTITY) for INSERT/UPDATE/DELETE matching
- [ ] Compare column list excludes `rowversion` / distinct clocks
- [ ] `core` schema created (`40-create-core-schema.sql`)
- [ ] `core` tables cloned (`41` / `usp_EmitCloneTableDdl`) — **no historical backfill**; core receives **SP writes only**
- [ ] `core` SP has the same parameters and writes only `core` objects
- [ ] DualWritePair registered (T0 stamped; `DboMaxIdAtStart` captured if `Id` exists)
- [ ] App route `ParallelWrite` (or generated `ParallelDboCoreWriter`) calls both SPs
- [ ] dbo failure fails the caller; core failure is evidence only
- [ ] OpenTelemetry meter `BuildingBlocks.Migration.ParallelWrite` visible
- [ ] Structured logs include operation, business key, correlation id, dbo/core ms — **no parameter values**
- [ ] `core.usp_TableIntegrity_Check` scheduled; evidence in `core.DualWriteEvidence`
- [ ] Hourly rollup `core.usp_RollupDualWriteMetricsHourly` (kit) or dashboard p95
- [ ] Extra dbo writers (EF, jobs, ad-hoc SQL, other SPs) are expected — integrity is **core ⊆ dbo**, not equality
- [ ] RBAC: runtime execute on dbo+core; integrity execute for operators (`45-dual-write-rbac.sql`)
- [ ] Mismatch rate 0 meaning **no core SP-written row missing/different in dbo** (dbo extras do not fail)
- [ ] Domain owner + DBA + security sign-off
- [ ] Rollback rehearsed: stop `ParallelWrite` (SourceFacade); `Enabled = 0`; retain `core` for investigation
- [ ] EF migrations project does **not** own the cloned tables
