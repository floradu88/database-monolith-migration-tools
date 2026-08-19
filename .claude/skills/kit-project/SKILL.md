---
name: kit-project
description: Project-scoped guidance; follow nearest `AI-INSTRUCTIONS.md` before editing within a kit folder.
---

## Purpose
Use this when the user asks you to make changes (code/docs/tests/manifests/RBAC/observability) inside a specific kit subfolder such as `sql/`, `manifests/`, `src-templates/`, `checklists/`, or `validation/`.

## Mandatory workflow
1. Read the root `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md` (for canonical context).
2. Read the nearest folder `AI-INSTRUCTIONS.md` for the area you will edit (and use `AI-INSTRUCTION-INDEX.md` to confirm scope).
3. Follow safety rules:
   - Prefer additive, reversible changes; preserve ownership boundaries.
   - Do not invent credentials, approvals, platform capabilities, or production values.
   - Never execute destructive SQL automatically.
   - Keep SQL database projects and EF Core migrations projects non-overlapping.
4. When behavior changes, update all related kit artifacts together:
   - docs, manifests, tests, RBAC, observability, and rollback guidance.
5. For local DbIntelligence / CodegraphChat work:
   - prefer PowerShell scripts under `src-templates/DbIntelligence/scripts/` and `src-templates/CodegraphChat/scripts/` (path-only “Ready” scripts),
   - prefer user-scoped Node via fnm where the scripts indicate it,
   - prefer Codegraph installation via `fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph`.
6. End with a completion report: changed files, assumptions, validation performed, unresolved risks, required human approvals.

## Response contract
- Start by stating which folder(s) you will treat as in-scope.
- Call out any assumptions or environment-specific gaps explicitly.

