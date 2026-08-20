# YAML Topology

Recursive YAML repository mapper that writes **one Markdown file with an embedded MermaidJS topology diagram**, including **dependency links** between resources.

Part of the SQL DB modernization kit as a discovery aid for infrastructure / pipeline / compose / manifest YAML trees.

## What it maps

Schema-aware adapters emit deterministic dependency edges for:

| Adapter | Dependency sources |
|---------|-------------------|
| Docker Compose | `depends_on`, `links`, networks, volumes |
| Kubernetes | `ownerReferences`, ConfigMap/Secret refs, volume mounts |
| GitHub Actions | `jobs.*.needs`, `uses` |
| Azure DevOps | stage/job `dependsOn` |
| CloudFormation | `DependsOn`, `Ref` / `Fn::GetAtt` |
| Kit manifests | domain ↔ wave, databases, services, SQL projects |
| Generic | common identity/reference keys + structured `depends_on` lists |

Missing targets become **dashed stub nodes** so links still appear (`-NoStubs` to disable).

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

Scan this kit’s manifests (domain ↔ wave links):

```powershell
.\run-topology.ps1 `
  -Repo "..\..\manifests" `
  -Output ".\out\manifests-topology.md" `
  -Title "Kit Manifests Topology"
```

Sample fixtures (Compose / K8s / GHA / CFN):

```powershell
.\run-topology.ps1 -Repo ".\fixtures" -Output ".\out\fixtures-topology.md" -Direction TB
```

Full CLI options: [`TOOLING.md`](TOOLING.md).

## Files

| File | Role |
|------|------|
| `yaml-topology.py` | Scanner, schema adapters, Mermaid + Markdown generator |
| `run-topology.ps1` | Non-admin wrapper (local `.venv` + PyYAML + CLI) |
| `fixtures/` | Small samples that exercise dependency edges |
| `TOOLING.md` | Detailed operator guide |
| `AI-INSTRUCTIONS.md` | Agent editing rules for this folder |

## Safety

- Reads YAML only (`yaml.safe_load_all`); does not execute YAML or call cloud APIs
- Does not modify source YAML; writes only the requested output path
- Treat diagrams as discovery maps — inspect before sharing (service names, hosts, endpoints may appear)
