# MigrationControlPlane — incremental roadmap (scaffold)

This folder is a **template shell**, not a full product. Showcase demonstrates wave *behavior* (routing flags, blue/green slots) without requiring this control plane. Ship milestones below only when orchestration beyond Showcase flags is needed.

Canonical architecture notes: [`../../docs/05-migration-control-plane.md`](../../docs/05-migration-control-plane.md) · future product: [`../../docs/FUTURE-FEATURES.md`](../../docs/FUTURE-FEATURES.md) §11.

## Wave A — Waves database (scaffold)

| Milestone | Intent | Status in kit |
|-----------|--------|---------------|
| A1 | SQL project / schema stubs for wave definitions, object ownership links, cutover ledger | Template / docs |
| A2 | Contract tables for wave state (Planned → Shadow → Canary → Cutover → Rollback) | Scaffold only |
| A3 | No auto-apply of cutover SQL; human + DBA gates remain mandatory | By design |

## Wave B — Control-plane API

| Milestone | Intent | Status in kit |
|-----------|--------|---------------|
| B1 | `MigrationControlPlane.Api` endpoints for wave CRUD / status (contracts project) | Shell |
| B2 | AuthZ aligned with kit RBAC docs — no invented IdP secrets | Placeholder |
| B3 | Read Showcase / FindingsMigration manifests; never dual-own EF vs SQL projects | Guidance only |

## Wave C — Worker / orchestration

| Milestone | Intent | Status in kit |
|-----------|--------|---------------|
| C1 | `MigrationControlPlane.Worker` host for scheduled reconciliation / health polls | Shell |
| C2 | CDC / dual-read orchestration hooks (advisory) | Not implemented |
| C3 | Emit observability events; no silent production cutover | Non-goal to automate |

## Suggested order of work

1. Prove domain packaging with **FindingsMigration** + **ShowcaseDataService**.
2. Use Showcase `MigrationRouting` / slot headers for lab cutover rehearsal.
3. Only then flesh Waves DB → API → Worker against real lab dual databases and checklists under `checklists/`.

## Non-goals

- Auto-approve ownership or production cutover.
- Invent credentials, cloud resources, or traffic weights.
- Replace DbIntelligence discovery or FindingsMigration packaging.

## Related

- Parent [`README.md`](README.md)
- Showcase cutover demo: [`../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md`](../DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md)
- Root [`../../TODO.md`](../../TODO.md)
