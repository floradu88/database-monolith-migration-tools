# AI Instructions — `/`

## Purpose

This folder is part of the reviewed SQL Server monolith-decomposition and DB-as-a-Service kit. Its role is determined by its path and local README/documentation.

## Mandatory workflow

1. Read the root `README.md`, `REVIEW-REPORT.md`, and nearest parent instructions.
2. Preserve ownership boundaries and prefer additive, reversible changes.
3. Do not invent credentials, approvals, platform capabilities, or production values.
4. Update docs, manifests, tests, RBAC, observability, and rollback guidance when behavior changes.
5. Never execute destructive SQL automatically.
6. Validate SQL-project and EF-migration ownership do not overlap.

## Current subfolders

- `checklists/`
- `docs/`
- `manifests/`
- `sql/`
- `src-templates/`
- `validation/`

## Current files

- `AGENTS.md`
- `AI-INSTRUCTION-INDEX.md`
- `CLAUDE.md`
- `HOW-TO-USE.md` — PowerShell setup/run guide (DbIntelligence, fnm Node, batch index, findings)
- `LICENSE`
- `README.md`
- `REVIEW-REPORT.md`
- `SOURCES.md`
- `V2-ADDENDUM.md`


## Agent tooling

- Cursor: `.cursor/rules/` (`kit-core`, `docs`, `sql`, `manifests`, `src-templates`, `checklists`, `validation`)
- Claude Code: `CLAUDE.md`
- Shared entry: `AGENTS.md`

## Completion report

Report changed files, assumptions, validation, unresolved risks, and required approval.
