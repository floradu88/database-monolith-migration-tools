---
name: kit-root
description: General, repository-wide guidance for this SQL DB modernization kit.
---

## Purpose
Use this when the user asks for repo-wide planning, navigation, safety/compliance reminders, or guidance that is not tied to a single sub-project folder.

## Mandatory workflow
1. Read `README.md`, `HOW-TO-USE.md`, `REVIEW-REPORT.md`, and `docs/CANONICAL-DOCUMENT-INDEX.md`.
2. Prefer canonical docs listed under “Start here” in the root README over supplemental references.
3. Preserve ownership boundaries; prefer additive, reversible changes.
4. Never invent credentials, approvals, production values, or environment-specific secrets.
5. Never execute destructive SQL automatically; treat `sql/` as DBA-review material.
6. Keep SQL database projects and EF Core migrations projects non-overlapping.
7. When behavior changes, update docs/manifests/tests/RBAC/observability/rollback guidance together.
8. Finish with a completion report: changed files, assumptions, validation performed, unresolved risks, and required approvals.

## How to respond
- If the request implies a specific folder (example: `src-templates/DbIntelligence`, `sql/`, `manifests/`), switch to the `kit-project` skill behavior by reading the nearest `AI-INSTRUCTIONS.md`.

