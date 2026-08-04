# AI Instructions — `src-templates/CodegraphChat/scripts`

## Purpose

PowerShell operator entrypoints for CodegraphChat (API + Angular UI).

## Rules

1. Reuse `../DbIntelligence/scripts/Initialize-DbIntelligenceNode.ps1` for Node/fnm/Codegraph — do not invent alternate installers.
2. Prefer Codegraph via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present.
3. ASCII punctuation only in `.ps1` strings (Windows PowerShell 5.1).
4. Never invent production paths or credentials; callers pass a local mapped repo path.
