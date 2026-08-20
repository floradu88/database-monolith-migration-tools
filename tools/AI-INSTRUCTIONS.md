# AI Instructions — `tools`

## Purpose

Operator utilities that are not .NET scaffolds. Prefer path-local PowerShell wrappers; do not invent admin installs or production credentials.

## Current subfolders

- `yaml-topology/` — recursive YAML → Mermaid topology Markdown (see folder `README.md` / `TOOLING.md`)

## Mandatory workflow

1. Read the nearest folder `AI-INSTRUCTIONS.md` before editing a tool.
2. Prefer additive, reversible changes; update root `HOW-TO-USE.md` when operator entrypoints change.
3. Never execute destructive SQL; these tools are discovery/docs generators only.

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
