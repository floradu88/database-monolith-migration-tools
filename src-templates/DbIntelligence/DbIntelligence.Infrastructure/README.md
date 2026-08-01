# DbIntelligence.Infrastructure

CLI runners (Codegraph / Graphify), evidence merge, in-memory store, prerequisite health/install, project discovery, and indexing orchestration.

## Operator notes

- Local setup is PowerShell-first under [`../scripts/`](../scripts/).
- Node/npm: user-scoped **fnm** via `Initialize-DbIntelligenceNode.ps1` (no admin).
- Codegraph: prefer `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph` when fnm is present (`PrerequisiteInstaller` follows the same order).
- Live graph/maps are **in memory**; durable output is JSON/MD export.

Docs: [`../README.md`](../README.md) · root [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
