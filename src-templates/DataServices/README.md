# DataServices

Target data-service templates.

| Template | Role |
|----------|------|
| **ShowcaseDataService** | Golden, buildable DB-as-a-Service template (FindingsMigration scaffold source) |
| **CustomerDataService** | Thin example only — not production-complete |

Keep SQL database projects and EF migrations projects non-overlapping.

## Why Showcase

See [`../../docs/PROJECT-GUIDE.md`](../../docs/PROJECT-GUIDE.md) for kit-wide pros/cons. Showcase is the replicable cutover shape: hybrid SQL/EF ownership, Shadow/Blue-Green, and **OnPrem / Azure / Aws** hosting ([`ShowcaseDataService/DATABASE-HOSTING.md`](ShowcaseDataService/DATABASE-HOSTING.md)).

## Related

- [`ShowcaseDataService/README.md`](ShowcaseDataService/README.md)
- [`ShowcaseDataService/SHOWCASE-CUTOVER.md`](ShowcaseDataService/SHOWCASE-CUTOVER.md)
- [`ShowcaseDataService/DATABASE-HOSTING.md`](ShowcaseDataService/DATABASE-HOSTING.md)
- [`CustomerDataService/README.md`](CustomerDataService/README.md)
- [`../FindingsMigration/README.md`](../FindingsMigration/README.md)
- Root [`../../HOW-TO-USE.md`](../../HOW-TO-USE.md) · [`../../README.md`](../../README.md) · [`../../docs/PROJECT-GUIDE.md`](../../docs/PROJECT-GUIDE.md)
