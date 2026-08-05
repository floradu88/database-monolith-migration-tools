# Not implemented — backlog for later

Snapshot of gaps after Showcase / reference-locations work (`b3f37f8`).  
Planned product features also live in [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md).

Nothing here is production cutover automation. Prefer additive, reversible changes.

---

## DbIntelligence (near-term polish)

- [x] **Web UI for reference locations** — References tab on graph page binds `GET /api/maps/code-references` (filter/sort/copy location).
- [x] **Check in / document the reference-locations canvas in-repo** — [`src-templates/DbIntelligence/docs/reference-locations-canvas.md`](src-templates/DbIntelligence/docs/reference-locations-canvas.md) (operator table + API/JSON load notes).
- [x] **Live canvas data** — Angular References / Code→DB tabs load live API data after index/export; offline table template in the canvas doc.
- [x] **Ignore local scan artifacts** — root `.gitignore` includes `.codegraph/`, `graphify-out/`, `.db-index/`.
- [x] **Angular “Promote to domain” UX** — multi-select on Code→DB / References + `POST /api/findings/promote` downloads promote-request JSON; run FindingsMigration.Cli locally (no API shell-out) ([FUTURE-FEATURES §6](docs/FUTURE-FEATURES.md)).
- [x] **Incremental re-index diff** — diff last two `code-to-db-map.json` exports; package only new EXTRACTED edges ([§7](docs/FUTURE-FEATURES.md)).
- [x] **Large-repo Graphify policy** — skip refresh when `graphify-out` exists; background refresh; noise filters ([§10](docs/FUTURE-FEATURES.md)).
- [ ] **Findings catalog database** — today maps are in-memory + JSON only; optional durable catalog later ([§5](docs/FUTURE-FEATURES.md)).

---

## FindingsMigration / domain packaging

- [x] **Domain suggestion from graph communities** — `findings-migrate suggest-domains --graph` (advisory; packaging still requires `--domain`).
- [x] **CI confidence gates** — `.github/workflows/ci.yml` + `confidence-gate` CLI; AMBIGUOUS ack via `validation/AMBIGUOUS-ACK.md`.
- [x] **Richer SP-centric packaging** — wrappers/stubs + `migration-manifest.snippet.yml` per procedure ([§3](docs/FUTURE-FEATURES.md)).
- [x] **SQL project slice generator** — additive SourceMonolith / target `*.Database` stubs from owned objects (hash + ownership only) ([§4](docs/FUTURE-FEATURES.md)).
- [x] **EF vs Dapper vs SP recommendation per operation** — attach hints from `docs/07-data-access-strategy.md` onto packaged API stubs ([§8](docs/FUTURE-FEATURES.md)).
- [x] **Reconciliation test stubs per promoted domain** — wire generated domains to `Tests/Reconciliation.Tests` patterns ([§9](docs/FUTURE-FEATURES.md)).

---

## Data services / BuildingBlocks

- [x] **CustomerDataService** — documented as non-golden / `NotImplementedException` thin example; Showcase is the scaffold source (README clarified).
- [x] **Showcase JWT / Managed Identity** — placeholder flags clarified (`Auth:RequireJwt` lab-off); [`AUTH.md`](src-templates/DataServices/ShowcaseDataService/AUTH.md) + Program/appsettings comments; no real IdP/MI secrets.
- [x] **Showcase SQL Pre/PostDeploy** — stubs remain; [`Scripts/README.md`](src-templates/DataServices/ShowcaseDataService/ShowcaseDataService.Database/Scripts/README.md) states human-gated apply only.
- [ ] **Real EKS/cloud cutover** — Helm + Compose blue/green templates exist; no live cluster, traffic weights, or cloud resource provisioning.
- [ ] **Other SourceMonolith / DataService folders** — mostly README/scaffold; not buildable golden paths (except Showcase).

---

## MigrationControlPlane

- [x] **Roadmap scaffold** — [`src-templates/MigrationControlPlane/ROADMAP.md`](src-templates/MigrationControlPlane/ROADMAP.md) documents Waves DB / API / worker milestones (docs only).
- [ ] **Full product** — waves DB, operators, CDC engine, orchestration API/worker — template shells/docs only. Showcase demonstrates wave *behavior* via flags + blue-green, not the control plane ([§11](docs/FUTURE-FEATURES.md)).

---

## Platform SQL / ops (kit scripts, human-gated)

- [x] **LocalDB lab apply guide** — [`sql/LAB-APPLY.md`](sql/LAB-APPLY.md) (sqlcmd against LocalDB only; discovery-safe script list). Harden against a real lab SQL Server still optional.
- [ ] Wire Query Store / XEvents / SQL Audit collection into DbIntelligence evidence beyond current repo scan + tool graphs.
- [ ] End-to-end lab cutover rehearsal using checklists under `checklists/` with real dual DBs.

---

## Explicit non-goals (do not implement as “auto”)

- Auto-approve ownership or production cutover.
- Invent production connection strings, credentials, or cloud resources.
- Overlap EF migrations ownership with SQL database project ownership.
- Blind execution of kit `sql/` scripts.
- Completing CustomerDataService as a second golden (Showcase is the scaffold source).

---

## Suggested next picks

1. Lab cutover rehearsal with `checklists/` + dual LocalDB databases.
2. Wire Query Store / XEvents / SQL Audit into DbIntelligence evidence (beyond repo scan).
3. MigrationControlPlane product work only when wave orchestration is needed beyond Showcase flags (see ROADMAP.md).
4. Optional findings catalog database (maps remain JSON/in-memory today).
