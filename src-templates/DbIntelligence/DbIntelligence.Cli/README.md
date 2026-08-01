# DbIntelligence.Cli

Command-line entry for prerequisite **health** and interactive **installs** (`--install-preqs`).

```powershell
cd ..
dotnet run --project .\DbIntelligence.Cli -- --health
dotnet run --project .\DbIntelligence.Cli -- --install-preqs --yes
```

Or via script (also provisions user-scoped Node + Codegraph via fnm first):

```powershell
.\scripts\Install-DbIntelligencePrereqs.ps1 -Yes
```

When installing Codegraph, **fnm is preferred** if on PATH:

```text
fnm exec -- npm i -g @colbymchenry/codegraph
```

Fallback: PATH `npm i -g`, then the official Codegraph install script.

Ops guide: [`../README.md`](../README.md) · [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
