# ShowcaseDataService authentication (lab)

Local demo defaults keep JWT **off**. Do not invent production secrets, client IDs, or IdP tenants in this kit.

## Defaults

| Setting | Lab default | Meaning |
|---------|-------------|---------|
| `Auth:RequireJwt` | `false` | `/api/showcase/*` is open for local demos |
| `Auth:Authority` | empty | Set only when wiring a real IdP |
| `Auth:Audience` | `showcase-dataservice` | Placeholder audience string |
| `Auth:ManagedIdentityClientId` | empty | Placeholder for Azure MI / app id |

When `RequireJwt` is `false`, `Program.cs` skips authentication middleware and does not call `RequireAuthorization()` on the showcase group.

## Enabling JWT (non-lab)

1. Set real values from your IdP / Key Vault — never commit them.
2. Point `Authority` / `Audience` at your tenant (see hosting notes below).
3. Set `Auth:RequireJwt` to `true` in the target environment (env vars or user-secrets).

```json
"Auth": {
  "RequireJwt": true,
  "Authority": "<your-idp-authority-url>",
  "Audience": "<your-api-audience>",
  "ManagedIdentityClientId": ""
}
```

## Managed Identity / SQL auth vs API JWT

- **API JWT** (`Auth:*`) protects HTTP endpoints.
- **Database auth** (`Database:Owned:AuthMode` / `SourceFacade:AuthMode`) is separate — see [`DATABASE-HOSTING.md`](DATABASE-HOSTING.md) for OnPrem / Azure / Aws connection patterns (`AzureManagedIdentity`, connection strings, etc.).

Do not confuse MI client IDs for SQL with JWT bearer authority URLs.

## Related

- `ShowcaseDataService.Api/Program.cs` — conditional JWT wiring
- `ShowcaseDataService.Api/appsettings.json` — placeholder `Auth` section
- [`DATABASE-HOSTING.md`](DATABASE-HOSTING.md) — provider + SQL auth modes
- Kit RBAC: [`../../../docs/09-rbac-security-and-change-control.md`](../../../docs/09-rbac-security-and-change-control.md)
