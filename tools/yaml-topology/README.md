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

## Quick start (one command — path only)

```powershell
cd D:\code\projects\database-monolith-migration-tools\tools\yaml-topology

.\Invoke-YamlTopologyReady.ps1 "D:\path\to\yaml-repo"
```

Creates the local `.venv`, installs PyYAML, scans recursively, writes `{repo}\topology.md` (Mermaid + Dependencies + **one explanation per YAML file**), and writes `topology-explains\*.explain.md` (one file each).

Optional flags: `-Direction LR` · `-Open` · `-NoStubs` · `-Adapters "compose,kubernetes,generic"` · `-Output "D:\out\topology.md"` · `-ExplainDir "D:\out\explains"` · `-SkipExplainFiles`

### Examples

```powershell
# Kit manifests (domain ↔ wave links + per-file explains)
.\Invoke-YamlTopologyReady.ps1 "..\..\manifests" -Output ".\out\manifests-topology.md"

# Sample fixtures
.\Invoke-YamlTopologyReady.ps1 ".\fixtures" -Output ".\out\fixtures-topology.md" -Open

# Explain a single YAML file (still emits one explain doc for that file)
.\Invoke-YamlTopologyReady.ps1 ".\fixtures\compose\docker-compose.yml" -Open
```

Lower-level wrapper (explicit params): `.\run-topology.ps1 -Repo ... -Output ...` — see [`TOOLING.md`](TOOLING.md).

## Files

| File | Role |
|------|------|
| `Invoke-YamlTopologyReady.ps1` | **One command** — path only → venv + scan + `topology.md` |
| `yaml-topology.py` | Scanner, schema adapters, Mermaid + Markdown generator |
| `run-topology.ps1` | Non-admin wrapper used by Ready (local `.venv` + PyYAML + CLI) |
| `fixtures/` | Small samples that exercise dependency edges |
| `TOOLING.md` | Detailed operator guide |
| `AI-INSTRUCTIONS.md` | Agent editing rules for this folder |

## Safety

- Reads YAML only (`yaml.safe_load_all`); does not execute YAML or call cloud APIs
- Does not modify source YAML; writes only the requested output path
- Treat diagrams as discovery maps — inspect before sharing (service names, hosts, endpoints may appear)
