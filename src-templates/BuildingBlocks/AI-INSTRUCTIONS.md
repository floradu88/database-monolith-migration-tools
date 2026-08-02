# AI Instructions — BuildingBlocks

Shared packages referenced by ShowcaseDataService. Prefer additive APIs.

## Mandatory

1. Do not invent credentials or production connection strings.
2. Runtime identities must not require `db_owner` (see Security guards).
3. Keep Migration types thin (route/slot/shadow) — no control-plane product here.
4. When changing observability attributes, align with NFR docs and update Showcase together.
