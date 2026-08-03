# Database hosting providers (OnPrem / Azure / Aws)

The Showcase golden template and BuildingBlocks connection helpers treat **on-premises SQL Server**, **Azure SQL**, and **AWS RDS/EC2 SQL Server** as first-class migration targets.

Configure everything under the single `Database` section (or `Database__*` env vars). Do **not** invent production credentials — use Key Vault / AWS Secrets Manager / user-secrets.

Kit context: [`docs/PROJECT-GUIDE.md`](../../../docs/PROJECT-GUIDE.md) · strategy: [`docs/04-target-database-project-strategy.md`](../../../docs/04-target-database-project-strategy.md)

---

## Why support all three

Migrations rarely jump straight to one cloud. Typical path:

1. **SourceFacade** stays **OnPrem** (monolith still authoritative).
2. **Owned** stands up on OnPrem lab, then **Azure** or **Aws** as the service database.
3. Shadow-compare across hosts → cut route → decommission façade.

One config model (`Provider` + auth + server/database) keeps the .NET service portable without rewriting data access.

---

## Providers at a glance

| Provider | Typical products | Default auth guidance |
|----------|------------------|------------------------|
| `OnPrem` | SQL Server, LocalDB, AG listener | `Integrated` or `SqlPassword` |
| `Azure` | Azure SQL DB, Managed Instance, SQL on Azure VM | `AzureActiveDirectoryDefault` or `AzureManagedIdentity` |
| `Aws` | RDS for SQL Server, SQL on EC2 | `SqlPassword` (secret-backed) |

---

## Pros and cons by provider

### OnPrem

**Best for:** monolith SourceFacade, early Owned lab, regulated data that must stay in-datacenter initially.

| Pros | Cons |
|------|------|
| Lowest friction next to today’s monolith | You own HA, patching, backups, DR |
| Windows integrated auth / AG patterns familiar to many DBAs | Scaling and elastic pools are manual |
| LocalDB fine for developer demos | Network coupling to datacenter for cloud apps |
| Easiest rollback during early FacadeThenMove | CapEx / ops cost may dominate long term |

**Choose OnPrem when:** ownership is still being proven, or cloud landing zone / identity is not ready.

### Azure

**Best for:** Owned databases in Microsoft estates, Managed Identity, elastic pools, Azure-native observability.

| Pros | Cons |
|------|------|
| Strong identity story (AAD / Managed Identity) — no SQL passwords in pods | Azure SQL DB feature gaps vs full SQL Server (know MI vs DB differences) |
| PaaS backups, patching, geo options | Egress / hybrid networking to OnPrem SourceFacade must be designed |
| Fits AKS + Key Vault + Private Link patterns in this kit’s Helm direction | Cost model (DTU/vCore/elastic pool) needs FinOps ownership |
| Encrypt + modern drivers are first-class in our composer/guards | Mis-set `Trusted_Connection` will fail validation (by design) |

**Choose Azure when:** the service will live in Azure and you want passwordless runtime auth.

### Aws

**Best for:** Owned databases on RDS/EC2 in AWS accounts; Secrets Manager–backed SQL logins.

| Pros | Cons |
|------|------|
| RDS managed backups/patching for SQL Server | SQL auth secrets must be rotated (no Azure-style MI for SQL in the same way) |
| Clear multi-AZ / snapshot story | Feature surface is “SQL Server on RDS” — version/edition limits apply |
| Works with EKS-style deploys and this kit’s Compose/Helm secrets pattern | Hybrid latency to OnPrem SourceFacade needs PrivateLink/VPN/Direct Connect |
| Composer defaults Encrypt=True for RDS endpoints | LocalDB is rejected for `Provider=Aws` (prevents fake “cloud” demos) |

**Choose Aws when:** the data service runtime is AWS-native and RDS is the approved store.

### Side-by-side

| Concern | OnPrem | Azure | Aws |
|---------|--------|-------|-----|
| Passwordless app auth | Integrated (Windows) | Managed Identity / AAD | Limited — prefer Secrets Manager + SqlPassword |
| Ops burden | High (you) | Lower (PaaS) | Medium (RDS) / High (EC2) |
| Typical Owned target after extract | Lab / interim | Common | Common |
| Typical SourceFacade during migrate | **Yes** | Rare for legacy monolith | Rare for legacy monolith |
| Kit guardrails | Warn if CS looks like cloud | Ban Trusted_Connection / Encrypt=False | Ban LocalDB; expect RDS host shape |

---

## Config shape

```json
"Database": {
  "Schema": "showcase",
  "Owned": {
    "Provider": "Azure",
    "AuthMode": "AzureActiveDirectoryDefault",
    "Server": "YOUR_SERVER.database.windows.net",
    "DatabaseName": "ShowcaseOwned",
    "ApplicationName": "ShowcaseDataService.Owned"
  },
  "SourceFacade": {
    "Provider": "OnPrem",
    "AuthMode": "Integrated",
    "Server": "sql-monolith",
    "DatabaseName": "MonolithDb",
    "ApplicationName": "ShowcaseDataService.SourceFacade"
  }
}
```

Or supply full connection strings (`Owned:ConnectionString` / legacy `OwnedConnectionString`) — they are still validated against `Provider`.

### Azure (example — no secrets in git)

```json
"Owned": {
  "Provider": "Azure",
  "AuthMode": "AzureManagedIdentity",
  "Server": "YOUR_SERVER.database.windows.net",
  "DatabaseName": "ShowcaseOwned",
  "ManagedIdentityClientId": ""
}
```

Env: `Database__Owned__Provider=Azure`, `Database__Owned__Server=...`  
Sample file: `ShowcaseDataService.Api/appsettings.Azure.json`

### Aws RDS (example — password from secret)

```json
"Owned": {
  "Provider": "Aws",
  "AuthMode": "SqlPassword",
  "Server": "YOUR_RDS.xxxxx.region.rds.amazonaws.com",
  "Port": 1433,
  "DatabaseName": "ShowcaseOwned",
  "UserId": "app_rw",
  "Encrypt": true,
  "TrustServerCertificate": false
}
```

Set `Database__Owned__Password` (or inject a full `ConnectionString`) from Secrets Manager — never commit passwords.  
Sample file: `ShowcaseDataService.Api/appsettings.Aws.json`

### OnPrem (lab / monolith source)

```json
"SourceFacade": {
  "Provider": "OnPrem",
  "AuthMode": "Integrated",
  "Server": "sql-monolith.contoso.local",
  "DatabaseName": "MonolithDb"
}
```

Default lab Owned/Source strings live in `appsettings.json` (LocalDB only).

---

## Validation

`SqlConnectionGuard` / `SqlConnectionStringComposer` reject common mismatches:

- `Provider=OnPrem` with `*.database.windows.net` or `*.rds.amazonaws.com`
- Azure + `Trusted_Connection=True`
- Azure + `Encrypt=False`
- Aws + LocalDB

---

## Deploy artifacts

- SQL project dacpac / EF migrations remain provider-agnostic T-SQL (SQL Server surface).
- Publish with `sqlpackage` / EF against the chosen endpoint after DBA review.
- Helm: set `database.provider` / `database.sourceProvider` and secret keys for connection material.

---

## Migration pattern

1. Keep **SourceFacade** on OnPrem monolith while **Owned** moves to Azure or Aws.
2. Shadow-compare reads across providers.
3. Cut route to Owned only after contract + RBAC + checklist approval.

See [`SHOWCASE-CUTOVER.md`](SHOWCASE-CUTOVER.md) and [`checklists/production-cutover.md`](../../../checklists/production-cutover.md).
