# Not implemented — backlog for later

Snapshot of gaps after Showcase / reference-locations work (`b3f37f8`).  
Planned product features also live in [`docs/FUTURE-FEATURES.md`](docs/FUTURE-FEATURES.md).

Nothing here is production cutover automation. Prefer additive, reversible changes.

---

## DbIntelligence (near-term polish)

- [ ] **Web UI for reference locations** — `GET /api/maps/code-references` and client types exist; graph/maps pages do not yet render the flat `fullPath` + `line` list (filter/sort/open-in-editor).
- [ ] **Check in / document the reference-locations canvas in-repo** — Cursor canvas lives under the local canvases folder; kit has no committed canvas copy for operators outside Cursor.
- [ ] **Live canvas data** — canvas uses example rows; optionally load from `code-reference-locations.json` or the API after export.
- [ ] **Ignore local scan artifacts** — untracked `.codegraph/` and `graphify-out/` under Contracts (and similar) should be gitignored kit-wide.
- [ ] **Angular “Promote to domain” UX** — select map rows / subgraph → FindingsMigration package write/download ([FUTURE-FEATURES §6](docs/FUTURE-FEATURES.md)).
- [ ] **Incremental re-index diff** — diff last two `code-to-db-map.json` exports; package only new EXTRACTED edges ([§7](docs/FUTURE-FEATURES.md)).
- [ ] **Large-repo Graphify policy** — skip refresh when `graphify-out` exists; background refresh; noise filters ([§10](docs/FUTURE-FEATURES.md)).
- [ ] **Findings catalog database** — today maps are in-memory + JSON only; optional durable catalog later ([§5](docs/FUTURE-FEATURES.md)).

---

## FindingsMigration / domain packaging

- [ ] **Domain suggestion from graph communities** — propose Billing/Onboarding/etc. from Graphify communities + path prefixes instead of a single `-DomainName` ([§1](docs/FUTURE-FEATURES.md)).
- [ ] **CI confidence gates** — fail PRs on missing owned-schema edges or rising AMBIGUOUS without review ack ([§2](docs/FUTURE-FEATURES.md)).
- [ ] **Richer SP-centric packaging** — wrappers/stubs shipped; fuller packaging aligned to `migration-manifest.example.yml` still open ([§3](docs/FUTURE-FEATURES.md)).
- [ ] **SQL project slice generator** — additive SourceMonolith / target `*.Database` stubs from owned objects (hash + ownership only) ([§4](docs/FUTURE-FEATURES.md)).
- [ ] **EF vs Dapper vs SP recommendation per operation** — attach hints from `docs/07-data-access-strategy.md` onto packaged API stubs ([§8](docs/FUTURE-FEATURES.md)).
- [ ] **Reconciliation test stubs per promoted domain** — wire generated domains to `Tests/Reconciliation.Tests` patterns ([§9](docs/FUTURE-FEATURES.md)).

---

## Data services / BuildingBlocks

- [ ] **CustomerDataService** — remains a thin example (`NotImplementedException` data access); do not treat as golden. Complete only if intentionally upgraded or remove/replace later.
- [ ] **Showcase JWT / Managed Identity** — placeholder flags only (`Auth:RequireJwt`); no real IdP/MI wiring or secrets.
- [ ] **Showcase SQL Pre/PostDeploy** — stubs + ownership attributes; no real DBA-approved deploy pipeline.
- [ ] **Real EKS/cloud cutover** — Helm + Compose blue/green templates exist; no live cluster, traffic weights, or cloud resource provisioning.
- [ ] **Other SourceMonolith / DataService folders** — mostly README/scaffold; not buildable golden paths (except Showcase).

---

## MigrationControlPlane

- [ ] **Full product** — waves DB, operators, CDC engine, orchestration API/worker — template shells/docs only. Showcase demonstrates wave *behavior* via flags + blue-green, not the control plane ([§11](docs/FUTURE-FEATURES.md)).

---

## Platform SQL / ops (kit scripts, human-gated)

- [ ] Apply / harden `sql/` discovery, telemetry, audit, RBAC scripts against a real lab SQL Server (never auto against production).
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

1. DbIntelligence Web list for `code-references` (closes the UI gap for the new JSON/API).
2. `.gitignore` for `.codegraph/` / `graphify-out/`.
3. Domain suggestion from communities **or** CI confidence gates (highest leverage on FindingsMigration).
4. MigrationControlPlane only when wave orchestration is needed beyond Showcase flags.
