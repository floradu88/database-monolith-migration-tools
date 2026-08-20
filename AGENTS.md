# Agent instructions

This repo is the **SQL Server Monolith Decomposition and DB-as-a-Service Kit (V5 Reviewed)**.

## Entry points

| Tool | Entry |
|------|--------|
| Claude Code | `CLAUDE.md` |
| Cursor | `.cursor/rules/` (project rules) |
| Any agent | Nearest `AI-INSTRUCTIONS.md` + `AI-INSTRUCTION-INDEX.md` |

## Mandatory workflow

1. Read root `README.md`, `HOW-TO-USE.md`, and `REVIEW-REPORT.md`.
2. Open the nearest folder `AI-INSTRUCTIONS.md` before editing that area.
3. Use canonical docs from the README “Start here” list; treat other numbered docs as supplemental unless the canonical index says otherwise.
4. Prefer additive, reversible changes; never invent production values or credentials.
5. Never execute destructive SQL automatically.
6. For DbIntelligence / CodegraphChat local setup, prefer PowerShell scripts under `src-templates/DbIntelligence/scripts/` and `src-templates/CodegraphChat/scripts/` (including user-scoped Node via fnm). Prefer Codegraph via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present. For YAML topology maps, prefer `tools/yaml-topology/Invoke-YamlTopologyReady.ps1` (path only).
7. End with a completion report: files changed, assumptions, validation, risks, approvals needed.


## Do not

- Blindly run kit SQL against production.
- Overlap EF migrations project ownership with SQL database project ownership.
- Silently drop or rewrite ownership boundaries in manifests or source-monolith projects.
