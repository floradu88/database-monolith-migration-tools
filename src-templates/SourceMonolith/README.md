# Source Monolith Database Projects

These projects represent the existing database without changing runtime behavior.

Recommended initial projects:

```text
Monolith.Database.Foundation
Monolith.Database.Customer
Monolith.Database.Billing
Monolith.Database.Ordering
Monolith.Database.Reference
Monolith.Database.Reporting
Monolith.Database.Integration
Monolith.Database.Legacy
Monolith.Database.Composite
```

Only `Monolith.Database.Composite` publishes the complete source database.

Each object definition must retain a source hash and ownership manifest entry.

## Discovery before split

Use **DbIntelligence** to index application repos that hit this monolith (PowerShell; see root [`HOW-TO-USE.md`](../../HOW-TO-USE.md)):

```powershell
cd ..\DbIntelligence
.\scripts\Initialize-DbIntelligenceNode.ps1 -Install -Yes
.\scripts\Setup-DbIntelligence.ps1 -Yes
.\scripts\Start-DbIntelligence.ps1 -Force
.\scripts\Invoke-DbIntelligenceIndex.ps1 -RepositoryPath "D:\path\to\app"
# batch parents: D:\code\projects or C:\code
```

Then promote maps with [`../FindingsMigration/`](../FindingsMigration/) as needed.
