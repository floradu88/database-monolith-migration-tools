# Kit project guide — what to use and why

This kit decomposes a shared SQL Server monolith into **ownership-bounded data services** without a big-bang rewrite. Use this page to understand **every major project area**, when to adopt it, and the trade-offs.

Related: [`HOW-TO-USE.md`](../HOW-TO-USE.md) · [`00-master-plan.md`](00-master-plan.md) · Showcase hosting [`DATABASE-HOSTING.md`](../src-templates/DataServices/ShowcaseDataService/DATABASE-HOSTING.md)

---

## Why use this kit

| Benefit | Why it matters |
|---------|----------------|
| **Evidence before ownership** | DbIntelligence maps code ↔ DB so you do not guess who owns a table or SP. |
| **Reversible cutover** | FacadeThenMove + Shadow + Blue/Green keep rollback cheap. |
| **Non-overlapping DDL** | SQL project vs EF migrations ownership is explicit — avoids dual-write of schema. |
| **Multi-host ready** | OnPrem, Azure, and AWS SQL targets are first-class for Owned vs SourceFacade. |
| **Agent-safe workflow** | `AI-INSTRUCTIONS.md` + checklists stop silent destructive SQL / invented credentials. |
| **Golden scaffold** | ShowcaseDataService is copyable; FindingsMigration turns maps into domain packages. |

### Kit-level trade-offs

| Pros | Cons |
|------|------|
| Incremental extraction; business keeps shipping | Requires discipline on manifests and dual-ownership bans |
| Works with existing T-SQL / SP estates | Not a turnkey “lift to microservices” product |
| Strong DBA-review posture | SQL scripts under `sql/` are review material — not auto-applied |
| Cloud-flexible (OnPrem → Azure/Aws) | Multi-provider networking, identity, and cost still need platform teams |
| AI/agent indexed workflow | Index quality depends on repo scan coverage and human approval of AMBIGUOUS findings |

**Use it when:** you have a large shared SQL Server, unclear SP/table ownership, and need a governed path to DB-as-a-Service.  
**Do not expect:** automatic production cutover, invented cloud resources, or dropping monolith objects without DBA gates.

---

## Project map (start → finish)

```text
DbIntelligence  →  FindingsMigration  →  Showcase / domain DataService
        ↑                                      ↓
 SourceMonolith (logical split)     BuildingBlocks + SQL project + EF
        ↓                                      ↓
     manifests / checklists          MigrationControlPlane (waves)
```

---

## 1. DbIntelligence (`src-templates/DbIntelligence/`)

**Purpose:** Discover how application code uses the database (tables, SPs, dynamic/templated names) and export evidence graphs and maps.

| Subproject | Role |
|------------|------|
| `DbIntelligence.Api` | HTTP API for index jobs, maps, health |
| `DbIntelligence.Cli` | Local health / prereq / scripted ops |
| `DbIntelligence.Worker` | Background indexing |
| `DbIntelligence.RepositoryScanner` | Roslyn scan of C# call sites (incl. `$"{a}_{b}"` SP templates) |
| `DbIntelligence.SqlScanner` | Live SQL inventory / dependency edges (optional connection) |
| `DbIntelligence.Infrastructure` | Merge Codegraph/Graphify/repo/SQL evidence |
| `DbIntelligence.Web` | Operator UI |
| `DbIntelligence.Contracts` / `Domain` / `Tests` | DTOs, model, regression |

**Why use it:** You get `code-to-db-map.json`, `stored-procedure-map.json`, and path/line references before any ownership claim. Templated SP names expand via enums/constants when present.

| Pros | Cons |
|------|------|
| Fast local one-shot (`Invoke-DbIntelligenceReady.ps1`) | Large repos need time / Graphify reuse |
| Combines static + optional live SQL | AMBIGUOUS findings still need humans |
| No admin Node via fnm | SQL scan needs a real connection string (secrets) |

---

## 2. FindingsMigration (`src-templates/FindingsMigration/`)

**Purpose:** Turn DbIntelligence maps into domain packages, manifests, SQL stubs, and Dapper SP wrappers; scaffold new services from Showcase.

**Why use it:** Bridges “we found it” → “we have a reviewable package and a service folder” without hand-copying dozens of files.

| Pros | Cons |
|------|------|
| Emits stubs + ownership YAML drafts | Stubs are not production SP bodies |
| Copies Showcase golden layout | Requires domain name + human FINDINGS-REVIEW |
| Registers Generated SQL into `.sqlproj` when present | AMBIGUOUS skipped unless explicitly included |

---

## 3. ShowcaseDataService (golden DataService)

**Purpose:** Buildable DB-as-a-Service template: hybrid SQL project + EF, fluent SP/SQL/EF access, Blue/Green, Shadow, configurable schema/connection, OnPrem/Azure/Aws hosts.

| Layer | Why it exists |
|-------|----------------|
| `*.Api` | HTTP + dashboard + route/slot headers |
| `*.Application` / `*.Domain` / `*.Contracts` | Clean boundaries; ownership attributes |
| `*.Infrastructure` | DI, DbContext, SP wrappers, `Database` options |
| `*.Database` | SSDT desired-state SPs/schemas/contract; Cutover up/down (None) |
| `*.Migrations` | EF-owned tables only |
| `deploy/` | Compose + Helm blue/green |

**Why use it:** Copy once per domain (or via FindingsMigration). One `Database` section for schema + provider; hybrid ownership already encoded.

| Pros | Cons |
|------|------|
| Production-shaped patterns without prod secrets | Demo LocalDB defaults are lab-only |
| SQL + EF split with tests guarding dual-own | SSDT publish needs sqlpackage/DBA |
| Multi-cloud Owned vs OnPrem SourceFacade | Platform identity (MI / Secrets Manager) is your job |

Deep dive: [`../src-templates/DataServices/ShowcaseDataService/README.md`](../src-templates/DataServices/ShowcaseDataService/README.md) · [`DATABASE-HOSTING.md`](../src-templates/DataServices/ShowcaseDataService/DATABASE-HOSTING.md) · [`SHOWCASE-CUTOVER.md`](../src-templates/DataServices/ShowcaseDataService/SHOWCASE-CUTOVER.md)

---

## 4. CustomerDataService

**Purpose:** Thin example of a domain service layout. **Not** the golden scaffold.

**Why it exists:** Historical / minimal reference. Prefer Showcase for new domains.

| Pros | Cons |
|------|------|
| Smaller surface to read | Incomplete vs Showcase |

---

## 5. BuildingBlocks (`src-templates/BuildingBlocks/`)

| Package | Why use it |
|---------|------------|
| `DataAccess.Abstractions` | Fluent contracts + `StoredProcedureName` templates |
| `DataAccess.Dapper` | `ExecuteSP` / `ExecuteSql` with timing |
| `DataAccess.EfCore` | Typed `UseShowcaseSqlServer` + EF fluent |
| `Migration` | Route / slot / shadow options |
| `Observability` | OTel hooks for ASP.NET / SQL |
| `Security` | Least-privilege guard + **OnPrem/Azure/Aws** connection composer |

| Pros | Cons |
|------|------|
| Shared without a heavy framework | Must keep versions aligned across services |
| Provider validation catches mis-pointed cutovers | Composer does not provision cloud resources |

---

## 6. SourceMonolith (`src-templates/SourceMonolith/`)

**Purpose:** Logical SQL projects by ownership slice (`Foundation`, `Customer`, `Billing`, …) plus `Composite` that publishes the full source DB.

**Why use it:** Organize the monolith for review and extraction **without** changing runtime behavior first.

| Pros | Cons |
|------|------|
| Ownership-shaped folders before physical move | Only Composite should publish “the whole DB” |
| Pairs with manifests | Split quality depends on discovery evidence |

---

## 7. MigrationControlPlane

**Purpose:** Wave planning, ledger-oriented control for multi-domain cutovers (API/Worker/Database templates).

**Why use it:** When one Showcase demo is not enough — coordinate many domains, statuses, and approvals.

| Pros | Cons |
|------|------|
| Central wave language | Needs wiring to real change systems |
| Aligns with manifests | Not a substitute for DBA change control |

---

## 7b. CodegraphChat (`src-templates/CodegraphChat/`)

**Purpose:** ChatGPT-style topic Q&A over a repository you already indexed with Codegraph.

| Subproject | Role |
|------------|------|
| `CodegraphChat.Api` | HTTP `:5091` — health, session bind, chat |
| `CodegraphChat.Infrastructure` | Codegraph CLI client + intent router |
| `CodegraphChat.Web` | Angular chat UI `:4201` |
| `scripts/` | PowerShell Ready/Start (reuses DbIntelligence fnm helper) |

**Why use it:** Conversational exploration of symbols, callers, callees, and impact without leaving the kit stack. Complements DbIntelligence (which builds maps) rather than replacing it. Prefer `Setup-CodegraphChat.ps1` / `Build-CodegraphChat.ps1` so the API can serve the SPA from `wwwroot` (single-host on `:5091`).

| Pros | Cons |
|------|------|
| Same .NET 8 + Angular 18 + PowerShell/fnm patterns | Best with an existing `.codegraph/` index (Ensure can init/sync) |
| Answers grounded in CLI evidence (no LLM key) | Not a code→DB map or ownership tool |
| Intent routing for query/callers/impact/status | Symbol detection benefits from quoting names |
| Single-host SPA publish into API wwwroot | Generated wwwroot assets are local build output |

---

## 8. Kit support folders (repo root)

| Path | Why use it |
|------|------------|
| `docs/` | Canonical architecture and strategy (start with numbered plan) |
| `sql/` | DBA-review discovery, audit, RBAC scripts — **never auto-run on prod** |
| `manifests/` | Domain / wave / object ownership examples |
| `checklists/` | Production cutover and source-split gates |
| `validation/` | Checksums / kit integrity |

---

## 9. Tests worth keeping green

| Area | Why |
|------|-----|
| Showcase `SqlProjectOwnershipTests` | Blocks dual-own of Items / Cutover Build mistakes |
| Showcase provider tests | OnPrem/Azure/Aws guardrails |
| DbIntelligence `RepositoryScannerTests` | Static + templated SP discovery |
| FindingsMigration generator tests | Stub + template expansion |

---

## Recommended adoption order

1. Run DbIntelligence on a real app path (`HOW-TO-USE.md`).
1b. Optional: explore the indexed repo via CodegraphChat topic chat.
2. Review maps; leave AMBIGUOUS on the queue.
3. FindingsMigration → domain package + Showcase-based scaffold.
4. Point `SourceFacade` at OnPrem monolith; land `Owned` on OnPrem lab, then Azure or Aws when ready ([`DATABASE-HOSTING.md`](../src-templates/DataServices/ShowcaseDataService/DATABASE-HOSTING.md)).
5. Shadow → Green → checklist → DBA-approved dacpac/EF.
6. Scale waves via MigrationControlPlane + manifests.

---

## What this kit is not

- A license to drop monolith objects without observation windows.
- An excuse to dual-own the same table in EF and SQL projects.
- A place to commit production connection strings or invent cloud subscriptions.
