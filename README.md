# SQL Server Monolith Decomposition and DB-as-a-Service Kit — V5 Reviewed

This repository contains the reviewed **sql-db-modernization-kit-v5** package: a production-grade path from a large shared SQL Server database to smaller, independently owned data services and databases.

It includes:

- discovery and AI-assisted repository indexing;
- SQL procedure, function, view, trigger, and DML-access tracking;
- Query Store, SQL Audit, Extended Events, and DMV collection;
- source-monolith decomposition into manageable SQL projects;
- target database projects and optional EF Core migrations;
- EF Core versus Dapper versus stored-procedure evaluation;
- migration control plane and migration manifests;
- RBAC, deployment controls, drift detection, and auditability;
- performance baselines, SLOs, observability, read scaling, elastic pools, and sharding;
- shadow reads, canaries, cutover, rollback, and decommissioning;
- example .NET solution/project templates and SQL scripts.

## Repository layout

```text
.
├── AGENTS.md / CLAUDE.md     # Claude Code + general agent entrypoints
├── .cursor/rules/            # Cursor project rules
├── AI-INSTRUCTIONS.md        # Root agent instructions (every folder has one)
├── AI-INSTRUCTION-INDEX.md   # Index of all AI-INSTRUCTIONS.md files
├── docs/                     # Architecture, plans, runbooks
├── sql/                      # Discovery, telemetry, audit, RBAC scripts
├── manifests/                # Domain + migration-wave examples
├── src-templates/            # .NET / SQL project scaffolds
├── checklists/               # Cutover and split checklists
└── validation/               # Checksums + validation summary
```

## Start here

1. `docs/00-master-plan.md`
2. `docs/01-target-architecture.md`
3. `docs/02-solution-and-project-structure.md`
4. `docs/03-source-monolith-split.md`
5. `docs/04-target-database-project-strategy.md`
6. `docs/05-migration-control-plane.md`
7. `docs/06-usage-tracking-and-audit.md`
8. `docs/07-data-access-strategy.md`
9. `docs/08-performance-monitoring-and-scaling.md`
10. `docs/09-rbac-security-and-change-control.md`
11. `docs/10-execution-roadmap.md`

## Cursor and Claude support

This kit is wired for AI-assisted work in **Cursor** and **Claude Code**:

| Tool | What to use |
|------|-------------|
| **Cursor** | Project rules in `.cursor/rules/` (always-on kit core + scoped rules for `docs/`, `sql/`, `manifests/`, `src-templates/`, `checklists/`, `validation/`) |
| **Claude Code** | Root `CLAUDE.md` |
| **Any agent** | `AGENTS.md`, plus the nearest folder `AI-INSTRUCTIONS.md` |

Every folder contains an `AI-INSTRUCTIONS.md` describing purpose, safety rules, how to modify that folder, and current contents. See `AI-INSTRUCTION-INDEX.md` for the full list.

Agents should:

1. read the root README, `REVIEW-REPORT.md`, and nearest `AI-INSTRUCTIONS.md`;
2. prefer additive, reversible changes and preserve ownership boundaries;
3. never invent credentials or production values;
4. never execute destructive SQL automatically;
5. finish with a completion report (files, assumptions, validation, risks, approvals).

## Important scope

The first tracking rollout records:

- DML statement execution;
- access to stored procedures, functions, views, and triggers;
- caller identity and application attribution;
- query performance and plan history;
- database object, permission, and deployment changes.

It does not initially capture old and new row values.

## Architecture principle

Decompose in this order:

```text
Discover
→ assign ownership
→ split source definitions into domain projects
→ introduce dedicated identities and namespaces
→ place a service contract in front
→ migrate schema and data
→ shadow and reconcile
→ cut over
→ revoke legacy access
→ physically scale or shard when justified
```

## Review status

This V5 package has been reviewed for structural consistency. See:

- `REVIEW-REPORT.md`
- `docs/CANONICAL-DOCUMENT-INDEX.md`
- `validation/validation-summary.json`

The numbered documents listed in the root “Start here” section are canonical. Earlier-generation documents are retained as supplemental references and identified in the canonical index.

SQL scripts are ready for DBA review, not blind production execution. Production deployment remains blocked until platform ADR, permissions, retention, and environment values are approved.
