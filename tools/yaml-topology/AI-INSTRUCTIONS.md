# AI Instructions — `tools/yaml-topology`

## Purpose

Recursive YAML → Mermaid topology Markdown generator for repository discovery. Complements DbIntelligence/CodegraphChat; it does **not** replace code→DB maps or ownership manifests.

## Mandatory workflow

1. Read `README.md` and `TOOLING.md` before changing behavior.
2. Prefer the PowerShell wrapper `run-topology.ps1` for operators (local `.venv`, no admin).
3. Keep dependencies limited to **PyYAML** in the local virtual environment — do not add Graphviz, Docker, or system-wide packages.
4. Do not invent cloud credentials, cluster endpoints, or production hostnames in samples/docs.
5. Prefer schema adapters for deterministic dependency edges; keep generic heuristics as fallback. Generated topology remains discovery-only — never promote to authoritative ownership/cutover evidence without human review.
6. Prefer additive changes; update `README.md` / `TOOLING.md` / root `HOW-TO-USE.md` together when CLI flags or paths change.

## Current files

- `yaml-topology.py`
- `run-topology.ps1`
- `TOOLING.md`
- `README.md`
- `fixtures/` (Compose / K8s / GHA / CFN samples)
- `.gitignore` (local `.venv` / generated `out/`)

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
