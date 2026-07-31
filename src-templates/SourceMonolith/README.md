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
