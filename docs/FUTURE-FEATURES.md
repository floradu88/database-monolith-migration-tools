# Future features — DbIntelligence → domain projects

Roadmap for turning **JSON mapping findings** (`code-to-db-map.json`, `stored-procedure-map.json`, unified `graph.json`) into **separate, ownership-bounded projects** inside this kit via templates.

Operational how-to today: [`HOW-TO-USE.md`](../HOW-TO-USE.md) · generator: [`src-templates/FindingsMigration/`](../src-templates/FindingsMigration/).

---

## Goal

```text
DbIntelligence index (repo path)
  → in-memory evidence graph (API process)
  → optional file export: graph.json + code-to-db-map.json + stored-procedure-map.json
  → FindingsMigration (review + package)  [reads JSON files, not a DB]
  → manifests/domains + migration-waves + object manifests
  → scaffolded DataServices/{Name}DataService from CustomerDataService template
  → human ownership approval → MigrationControlPlane waves
```

Nothing in this path auto-cuts over production. AMBIGUOUS findings stay on a review queue. **Mapping storage is in-memory (+ JSON files), not a database.**

---

## Near-term (template shipped)

| Feature | Status | Notes |
|---------|--------|--------|
| Import `code-to-db-map.json` / SP map | **Shipped (v1)** | `FindingsMigration.Cli` |
| Emit domain YAML + object migration stubs | **Shipped (v1)** | Under output folder / `manifests/` |
| Scaffold DataService from Customer template | **Shipped (v1)** | PowerShell `New-DomainFromFindings.ps1` |
| Review queue for AMBIGUOUS edges | **Shipped (v1)** | `FINDINGS-REVIEW.md` |
| PowerShell-only operator flow | **Shipped (v1)** | See FindingsMigration README |

---

## Next features (planned)

### 1. Domain suggestion from graph communities
Cluster Graphify `community` + code path prefixes into proposed domains (e.g. `Billing`, `Onboarding`) instead of a single `-DomainName` flag.

### 2. Confidence gates in CI
Fail PR checks if new `EXTRACTED` code→DB edges for a owned schema are missing from the domain manifest, or if `AMBIGUOUS` count rises without review acknowledgements.

### 3. Stored-procedure–centric packaging
Prefer `stored-procedure-map.json` as the primary unit of migration (callers, tables read/written, façade candidates) aligned with `migration-manifest.example.yml`.

### 4. SQL project slice generator
From owned DB objects, emit additive SQL project stubs under `SourceMonolith` / target `*.Database` without moving definitions until approved (hash + ownership only).

### 5. Findings catalog database (optional — not current)

**Today:** maps live in API process memory; durability is JSON export only.

**Later:** persist maps into an intelligence catalog (entities in `docs/09-data-model-and-api.md`) instead of file-only JSON — still no production writes without DBA review. Until then, FindingsMigration always reads **exported JSON files**, not a database.

### 6. Angular “Promote to domain” UX
In DbIntelligence.Web: select subgraph / map rows → call FindingsMigration → download or write package under kit `manifests/` + `src-templates/DataServices/`.

### 7. Incremental re-index diff
Diff last two `code-to-db-map.json` exports; only package **new** EXTRACTED edges into the next wave.

### 8. EF vs Dapper vs SP recommendation per operation
Attach A/B hints from kit `docs/07-data-access-strategy.md` onto each packaged API operation stub.

### 9. Shadow-read / reconciliation stubs
Generate empty test projects wired to `Tests/Reconciliation.Tests` patterns for each promoted domain.

### 10. Large-repo Graphify policy
Default `refreshGraphify: false` when `graphify-out/graph.json` exists; background refresh job; exclude `node_modules` / build `chunk-*.js` noise from god-node reports.

---

## Non-goals (explicit)

- Auto-approve ownership or cutover.
- Invent production connection strings or cloud resources.
- Overlap EF migrations ownership with SQL database project ownership.
- Blind execution of kit `sql/` scripts.

---

## Acceptance for each future feature

1. Additive, reversible template or script change.
2. PowerShell documented in `HOW-TO-USE.md` / FindingsMigration README.
3. AMBIGUOUS findings never silently become owned objects.
4. Update nearest `AI-INSTRUCTIONS.md` when behavior ships.
