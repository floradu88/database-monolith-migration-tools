# Production Cutover Checklist

Use with the Showcase golden demo: [`src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](../src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md) (blue → shadow → green). This checklist is required before **real** production traffic.

- [ ] Source schema captured and hashed
- [ ] Source permissions captured
- [ ] All callers identified
- [ ] Unknown identity traffic resolved
- [ ] Target SQL project deployed
- [ ] EF migrations reviewed and applied
- [ ] No overlapping object ownership
- [ ] RBAC validated
- [ ] Query Store enabled
- [ ] SQL Audit enabled
- [ ] OpenTelemetry traces visible
- [ ] Performance baseline recorded
- [ ] Backfill complete
- [ ] Synchronization lag within threshold
- [ ] Reconciliation passed
- [ ] Shadow-read mismatch zero for critical fields
- [ ] Canary / blue-green weight switch approved (Showcase: Helm `ingress.greenWeight`)
- [ ] Canary SLOs passed
- [ ] Capacity and connection budget passed
- [ ] Backup restore tested
- [ ] Rollback rehearsed (route back to SourceFacade / blue weight 100)
- [ ] On-call owner confirmed
- [ ] Cutover approved
- [ ] Legacy-call alert enabled
