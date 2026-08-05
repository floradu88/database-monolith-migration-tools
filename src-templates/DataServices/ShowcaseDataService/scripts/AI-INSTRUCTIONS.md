# AI Instructions — ShowcaseDataService/scripts

## Purpose

Lab-only PowerShell helpers for LocalDB publish and SP export verification.

## Scripts

- `Initialize-ShowcaseLocalDb.ps1` — create ShowcaseOwned/ShowcaseSource, PreDeploy, EF Items, Programmability via sqlcmd, optional export assert

## Rules

1. Never invent production credentials.
2. Never auto-apply Cutover destructive waves.
3. Prefer sqlcmd lab path; `-UseSqlPackage` only when dacpac tooling exists.
4. Keep hybrid ownership: EF owns Items; SQL project owns SPs/contract.
