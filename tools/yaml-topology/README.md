# YAML Topology

Recursive YAML repository mapper that writes **one Markdown file with an embedded MermaidJS topology diagram**.

Part of the SQL DB modernization kit as a discovery aid for infrastructure / pipeline / compose YAML trees. Relationships are **heuristic**, not authoritative runtime state.

## Requirements

- Python 3.10+ on PATH
- PowerShell 5.1+ (wrapper only)
- No admin rights; PyYAML installs into a local `.venv` next to these scripts

## Quick start

```powershell
cd D:\code\projects\database-monolith-migration-tools\tools\yaml-topology

.\run-topology.ps1 `
  -Repo "D:\path\to\yaml-repo" `
  -Output "D:\path\to\yaml-repo\topology.md"
```

Scan this kit’s manifests (example):

```powershell
.\run-topology.ps1 `
  -Repo "..\..\manifests" `
  -Output ".\out\manifests-topology.md" `
  -Title "Kit Manifests Topology"
```

Full CLI options, Mermaid directions, and safety notes: [`TOOLING.md`](TOOLING.md).

## Files

| File | Role |
|------|------|
| `yaml-topology.py` | Scanner, relationship mapper, Mermaid + Markdown generator |
| `run-topology.ps1` | Non-admin wrapper (local `.venv` + PyYAML + CLI) |
| `TOOLING.md` | Detailed operator guide |
| `AI-INSTRUCTIONS.md` | Agent editing rules for this folder |

## Safety

- Reads YAML only (`yaml.safe_load_all`); does not execute YAML or call cloud APIs
- Does not modify source YAML; writes only the requested output path
- Treat diagrams as discovery maps — inspect before sharing (service names, hosts, endpoints may appear)
