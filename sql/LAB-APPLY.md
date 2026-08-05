# Platform SQL — LocalDB lab apply

**Never** point these scripts at production. Apply only against a disposable LocalDB (or dedicated lab) instance after DBA review of each file.

Kit scripts live under `sql/common/` (also `sql/azure-sql-db/`, `sql/sql-server-mi/` for platform-specific variants). This note covers a **human-gated** LocalDB walkthrough with `sqlcmd`.

## Prerequisites

```powershell
sqlcmd -?
# LocalDB instance example:
# Server=(localdb)\mssqllocaldb
```

Create or pick a **lab** database (example name only — replace safely):

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "IF DB_ID(N'DbIntelligenceLab') IS NULL CREATE DATABASE DbIntelligenceLab;"
```

## Apply a selected script

From the kit root:

```powershell
$server = "(localdb)\mssqllocaldb"
$db     = "DbIntelligenceLab"
$script = "sql\common\00-preflight.sql"

sqlcmd -S $server -d $db -E -i $script -b
```

`-b` exits non-zero on SQL error. Review output before chaining more scripts.

## Discovery-safe (read-oriented) scripts

These are primarily **SELECT / reporting** against catalog, DMVs, or Query Store. They still need review (permissions, PII in plans), but they do **not** create app schemas, alter Query Store, or insert telemetry by design:

| Script | Notes |
|--------|--------|
| `00-preflight.sql` | Server/DB/Query Store option readouts |
| `04-inventory-objects-and-dependencies.sql` | Object inventory + declared dependencies |
| `05-current-cache-reports.sql` | Current `dm_exec_*` cache stats |
| `07-query-store-procedure-analysis.sql` | Query Store module aggregates |
| `22-query-store-performance-baseline.sql` | QS performance baseline SELECT |
| `24-database-capacity-snapshot.sql` | File / index size SELECT |
| `28-schema-drift-hash.sql` | Definition hash SELECT |
| `30-post-migration-legacy-access-report.sql` | Report SELECT (assumes telemetry catalog exists) |

Suggested first lab pass: `00` → `04` → `05`.

## Not discovery-safe (writes / config / DDL)

Require explicit approval; do not batch-apply blindly:

| Script | Why gated |
|--------|-----------|
| `01-create-telemetry-schema.sql` | Creates telemetry/inventory objects |
| `02-enable-query-store.sql` | `ALTER DATABASE` Query Store |
| `03-snapshot-dmv-usage.sql` | INSERT into telemetry |
| `06-session-attribution.sql` / `29-session-context-bootstrap.sql` | Session context bootstrap (app wiring) |
| `20-create-deployment-ledger.sql` | Deployment ledger DDL |
| `21-create-rbac-roles.sql` | Role DDL (grants commented) |
| `23-object-and-dml-audit-spec-template.sql` | Audit spec template |
| `25-usage-aggregation-contract.sql` | Contract / aggregation DDL |
| `26-object-definition-snapshot.sql` / `27-permission-snapshot.sql` | Snapshot tables + inserts |
| `31-ddl-security-audit-template.sql` | Security audit template |

## Safety

- Prefer additive, reversible lab databases.
- Do not invent production connection strings or credentials.
- SqlScanner / DbIntelligence repo scan is separate from applying `sql/` scripts.
- Platform variants under `azure-sql-db/` and `sql-server-mi/` may differ — read each header before apply.

## Related

- `sql/common/AI-INSTRUCTIONS.md`
- Root [`../HOW-TO-USE.md`](../HOW-TO-USE.md)
- Showcase Pre/PostDeploy (separate SSDT scripts): `src-templates/DataServices/ShowcaseDataService/ShowcaseDataService.Database/Scripts/`
