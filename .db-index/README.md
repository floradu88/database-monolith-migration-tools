# `.db-index` — DbIntelligence export folder

DbIntelligence writes index findings here **automatically** when you run Ready / Index / Export / Combine.

Do not hand-author reports in this folder; re-run the tool to refresh:

```powershell
cd src-templates\DbIntelligence
.\scripts\Invoke-DbIntelligenceReady.ps1 "D:\path\to\your\app"
```

## Per indexed project (`{repo}/.db-index/`)

| File | Purpose |
|------|---------|
| `graph.json` | Unified evidence graph |
| `code-to-db-map.json` | Code → DB object map |
| `stored-procedure-map.json` | Stored procedure findings |
| `code-reference-locations.json` | Full path:line references |
| `GRAPH_REPORT.md` | Markdown report with Mermaid |
| `findings.html` | Standalone HTML report (open in a browser) |

## Batch parent

| Path | Purpose |
|------|---------|
| `{parent}/db-intelligence-batch-summary.json` | Batch job summary |
| `{parent}/.db-index-combined/` | Combined multi-project export (same file set) |

Override with `ArtifactsRelativeDirectory` / `ArtifactsDirectory` if needed; default is `.db-index`.
