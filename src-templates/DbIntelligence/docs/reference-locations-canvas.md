# Reference locations canvas (in-repo)

Operators can review **code → database reference locations** without relying on a Cursor-only canvas. This kit document mirrors the Angular **References** tab and the exported JSON/API surface.

## Where live data comes from

After you index a repository (and optionally **Export JSON**), DbIntelligence materializes reference rows from the in-memory evidence graph:

| Source | Path / endpoint | Notes |
|--------|-----------------|--------|
| Export artifact | `{repo}/.db-index/code-reference-locations.json` | Written by `POST /api/export` / CLI export |
| Live API | `GET /api/maps/code-references` | Same shape as the JSON export |
| Angular UI | Graph page → **References** tab | Loads **live API data** after index/export (not static sample rows) |

Related maps:

- `GET /api/maps/code-to-db` → `code-to-db-map.json`
- `GET /api/maps/stored-procedures` → `stored-procedure-map.json`

### Live canvas / References tab note

The Angular **References** tab binds `GET /api/maps/code-references` after an index job completes or after you open the tab (via `loadMaps()`). Empty tables mean no graph is loaded yet — run Ready/index or open an already-exported repo and re-index/export. Offline operators can paste rows into the markdown table below from `code-reference-locations.json` without running the UI.

## How to load offline from JSON

1. Index (PowerShell, from `src-templates/DbIntelligence`):

```powershell
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\repo"
```

2. Open `{repo}/.db-index/code-reference-locations.json` (or copy from a reviewed export).

3. Fill the operator table template below (or filter in Excel / VS Code).

Example row shape (abbreviated):

```json
{
  "fullPath": "D:\\repo\\src\\Orders\\OrderService.cs",
  "line": 42,
  "location": "D:\\repo\\src\\Orders\\OrderService.cs:42",
  "dbObject": "dbo.usp_GetOrder",
  "confidence": "EXTRACTED",
  "codeLabel": "GetOrderAsync"
}
```

## Angular References tab (operators)

1. Start API + Web per [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
2. Index a repo (or batch parent folder).
3. Open **References**.
4. Filter / sort / **Copy** location strings for IDE navigation.
5. Optionally multi-select rows and use **Promote to domain** (downloads a promote-request JSON; run FindingsMigration.Cli locally — the API does **not** shell out).

## Operator offline table template

Copy this table into a working note and fill from JSON or the UI. Leave AMBIGUOUS rows on a review queue unless explicitly approved.

| Full path | Line | Location | DB object | Confidence | Code label | Notes / owner |
|-----------|------|----------|-----------|------------|------------|---------------|
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |

### Confidence guidance

| Confidence | Operator action |
|------------|-----------------|
| `EXTRACTED` | Candidate for packaging after ownership review |
| `INFERRED` | Confirm before promote |
| `AMBIGUOUS` | Review queue only — never silent ownership |

## Related

- DbIntelligence README: [`../README.md`](../README.md)
- FindingsMigration: [`../../FindingsMigration/README.md`](../../FindingsMigration/README.md)
- Future UX notes: [`../../../docs/FUTURE-FEATURES.md`](../../../docs/FUTURE-FEATURES.md) §6
