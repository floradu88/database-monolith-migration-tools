#!/usr/bin/env python3
"""Recursive YAML repository topology mapper.

Reads *.yaml / *.yml files recursively and writes either:
- Markdown with a MermaidJS diagram (default for .md output), or
- raw Mermaid source (for .mmd output).

Schema-aware adapters extract deterministic dependency edges for:
Docker Compose, Kubernetes, GitHub Actions, Azure DevOps, CloudFormation,
and this kit's domain/wave manifests. A generic heuristic pass fills gaps.

Unresolved dependency targets become stub nodes so links still appear.
No Graphviz, Go, Docker, or administrator rights are required.
"""

from __future__ import annotations

import argparse
import re
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable

try:
    import yaml
except ImportError:
    raise SystemExit("Missing PyYAML. Run: python -m pip install pyyaml")

ID_KEYS = {"name", "id", "service", "app", "application", "component", "domain", "wave"}
REF_KEYS = {
    "dependson",
    "depends_on",
    "dependencies",
    "needs",
    "ref",
    "reference",
    "target",
    "service",
    "database",
    "queue",
    "topic",
    "cluster",
    "host",
    "endpoint",
    "backend",
    "upstream",
    "uses",
    "image",
    "source_database",
    "target_database",
    "target_service",
    "owner_team",
    "domain",
}
SKIP = {".git", ".venv", "venv", "node_modules", "bin", "obj", ".terraform", "out"}
ADAPTERS = (
    "compose",
    "kubernetes",
    "github-actions",
    "azure-devops",
    "cloudformation",
    "kit-manifest",
    "generic",
)


def scalar(value: Any) -> bool:
    return isinstance(value, (str, int, float, bool))


def clean_id(value: Any) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_]+", "_", str(value)).strip("_")
    if not cleaned:
        cleaned = "node"
    if cleaned[0].isdigit():
        cleaned = "n_" + cleaned
    return cleaned[:180]


def escape_label(value: Any) -> str:
    return str(value).replace("\\", "\\\\").replace('"', "'").replace("\n", " ")


def as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def walk(obj: Any, path: str = "", found: list | None = None) -> list[tuple[str, str, str]]:
    found = found or []
    if isinstance(obj, dict):
        for key, value in obj.items():
            current = f"{path}.{key}" if path else str(key)
            if scalar(value):
                found.append((str(key), str(value), current))
            else:
                walk(value, current, found)
    elif isinstance(obj, list):
        for index, value in enumerate(obj):
            walk(value, f"{path}[{index}]", found)
    return found


def load_documents(path: Path) -> list[Any]:
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            return list(yaml.safe_load_all(handle))
    except Exception as exc:
        print(f"WARN {path}: {exc}")
        return []


def discover_files(root: Path) -> list[Path]:
    return sorted(
        p
        for p in root.rglob("*")
        if p.is_file()
        and p.suffix.lower() in (".yaml", ".yml")
        and not any(part in SKIP for part in p.parts)
    )


class TopologyGraph:
    def __init__(self, *, create_stubs: bool = True) -> None:
        self.nodes: dict[str, dict[str, Any]] = {}
        self.edges: set[tuple[str, str, str]] = set()
        self.aliases: dict[str, set[str]] = defaultdict(set)
        self.unresolved: list[tuple[str, str, str]] = []
        self._used_ids: set[str] = set()
        self.create_stubs = create_stubs

    def add_node(
        self,
        *,
        preferred_id: str,
        label: str,
        file: str,
        name: str | None,
        kind: str,
        aliases: Iterable[str] | None = None,
        stub: bool = False,
    ) -> str:
        node_id = preferred_id
        suffix = 2
        while node_id in self._used_ids:
            node_id = f"{preferred_id}_{suffix}"
            suffix += 1
        self._used_ids.add(node_id)
        self.nodes[node_id] = {
            "label": label,
            "file": file,
            "name": name,
            "kind": kind,
            "stub": stub,
        }
        self.register_alias(node_id, name)
        self.register_alias(node_id, kind)
        if name:
            self.register_alias(node_id, f"{kind}/{name}")
            self.register_alias(node_id, f"{kind}:{name}")
        for alias in aliases or []:
            self.register_alias(node_id, alias)
        return node_id

    def ensure_stub(self, name: str, kind: str = "Missing") -> str:
        matches = self.resolve(name, prefer_exact=True)
        if matches:
            return matches[0]
        for node_id, node in self.nodes.items():
            if node.get("stub") and str(node.get("name", "")).lower() == name.lower():
                return node_id
        preferred = clean_id(f"stub_{kind}_{name}")
        return self.add_node(
            preferred_id=preferred,
            label=f"{kind}: {name} (unresolved)",
            file="(unresolved)",
            name=name,
            kind=kind,
            aliases=[name, f"stub_{kind}_{name}".lower()],
            stub=True,
        )

    def register_alias(self, node_id: str, alias: Any) -> None:
        if alias is None:
            return
        text = str(alias).strip()
        if not text:
            return
        self.aliases[text.lower()].add(node_id)

    def resolve(self, needle: Any, *, prefer_exact: bool = True) -> list[str]:
        if needle is None:
            return []
        text = str(needle).strip()
        if not text:
            return []
        key = text.lower()
        exact = sorted(self.aliases.get(key, set()))
        if exact:
            return exact
        if prefer_exact:
            if "/" in text:
                exact = sorted(self.aliases.get(text.rsplit("/", 1)[-1].lower(), set()))
                if exact:
                    return exact
            return []
        partial = []
        for alias, node_ids in self.aliases.items():
            if key in alias or alias in key:
                partial.extend(node_ids)
        return sorted(set(partial))

    def add_edge(
        self,
        source: str,
        target_name: Any,
        relation: str,
        *,
        create_stub: bool = True,
        stub_kind: str = "Dependency",
        allow_partial: bool = False,
    ) -> None:
        if source not in self.nodes:
            return
        needle = target_name
        if isinstance(target_name, dict):
            if len(target_name) == 1:
                needle = next(iter(target_name.keys()))
            else:
                needle = (
                    target_name.get("condition")
                    or target_name.get("service")
                    or next(iter(target_name.keys()), None)
                )
        matches = self.resolve(needle, prefer_exact=not allow_partial)
        if not matches and allow_partial:
            matches = self.resolve(needle, prefer_exact=False)
        if not matches:
            self.unresolved.append((source, relation, str(needle)))
            if not create_stub or not self.create_stubs:
                return
            matches = [self.ensure_stub(str(needle), stub_kind)]
        for destination in matches[:8]:
            if destination == source:
                continue
            self.edges.add((source, destination, relation))


def detect_adapter(document: Any, relative: str) -> str:
    if not isinstance(document, dict):
        return "generic"
    lower_path = relative.replace("\\", "/").lower()
    keys = {str(k).lower() for k in document.keys()}

    if "services" in keys and (
        "version" in keys
        or any(
            isinstance(v, dict) and ("image" in v or "build" in v or "depends_on" in v)
            for v in (document.get("services") or {}).values()
            if isinstance(v, dict)
        )
    ):
        return "compose"

    if "kind" in keys and (
        "apiVersion" in document or "api_version" in keys or "metadata" in keys
    ):
        return "kubernetes"

    if "jobs" in keys and (
        "on" in keys or "name" in keys or lower_path.startswith(".github/workflows/")
    ):
        return "github-actions"

    if ("stages" in keys or "jobs" in keys) and (
        "trigger" in keys
        or "pool" in keys
        or "extends" in keys
        or lower_path.endswith("azure-pipelines.yml")
    ):
        return "azure-devops"

    resources = document.get("Resources") or document.get("resources") or {}
    if "resources" in keys and (
        "AWSTemplateFormatVersion" in document
        or "awsTemplateFormatVersion" in document
        or any(
            isinstance(v, dict) and ("Type" in v or "type" in v)
            for v in resources.values()
            if isinstance(v, dict)
        )
    ):
        return "cloudformation"

    if "domain" in keys and (
        "owner_team" in keys
        or "target_service" in keys
        or "wave" in keys
        or "source_database" in keys
        or "/domains/" in lower_path
        or "/migration-waves/" in lower_path
    ):
        return "kit-manifest"

    if "wave" in keys and "domain" in keys:
        return "kit-manifest"

    return "generic"


def extract_depends_targets(value: Any) -> list[Any]:
    """Normalize depends_on / needs / DependsOn into target names."""
    targets: list[Any] = []
    if value is None:
        return targets
    if scalar(value):
        return [value]
    if isinstance(value, list):
        for item in value:
            targets.extend(extract_depends_targets(item))
        return targets
    if isinstance(value, dict):
        for key, nested in value.items():
            if isinstance(nested, dict) and set(str(k).lower() for k in nested.keys()) <= {
                "condition",
                "restart",
                "required",
            }:
                targets.append(key)
            elif key.lower() in {"service", "name", "job", "stage", "ref", "id"}:
                targets.append(nested)
            else:
                targets.append(key)
        return targets
    return targets


def apply_compose(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    services = document.get("services") or {}
    if not isinstance(services, dict):
        return
    service_ids: dict[str, str] = {}
    for name, spec in services.items():
        node_id = graph.add_node(
            preferred_id=clean_id(f"compose_{relative}_{name}"),
            label=f"ComposeService: {name}",
            file=relative,
            name=str(name),
            kind="ComposeService",
            aliases=[f"service/{name}", f"compose/{name}"],
        )
        service_ids[str(name)] = node_id
        if isinstance(spec, dict) and spec.get("image"):
            graph.register_alias(node_id, spec["image"])

    networks = document.get("networks") or {}
    if isinstance(networks, dict):
        for name in networks:
            graph.add_node(
                preferred_id=clean_id(f"compose_net_{relative}_{name}"),
                label=f"Network: {name}",
                file=relative,
                name=str(name),
                kind="Network",
                aliases=[f"network/{name}"],
            )

    volumes = document.get("volumes") or {}
    if isinstance(volumes, dict):
        for name in volumes:
            graph.add_node(
                preferred_id=clean_id(f"compose_vol_{relative}_{name}"),
                label=f"Volume: {name}",
                file=relative,
                name=str(name),
                kind="Volume",
                aliases=[f"volume/{name}"],
            )

    for name, spec in services.items():
        source = service_ids[str(name)]
        if not isinstance(spec, dict):
            continue
        for dep in extract_depends_targets(spec.get("depends_on")):
            graph.add_edge(source, dep, "depends_on", stub_kind="ComposeService")
        for link in as_list(spec.get("links")):
            target = str(link).split(":", 1)[0]
            graph.add_edge(source, target, "links", stub_kind="ComposeService")
        for net in as_list(spec.get("networks")):
            if isinstance(net, dict):
                for net_name in net:
                    graph.add_edge(source, net_name, "network", stub_kind="Network")
            else:
                graph.add_edge(source, net, "network", stub_kind="Network")
        for vol in as_list(spec.get("volumes")):
            if isinstance(vol, str) and not vol.startswith(".") and not vol.startswith("/"):
                vol_name = vol.split(":", 1)[0]
                if vol_name and "/" not in vol_name:
                    graph.add_edge(source, vol_name, "volume", stub_kind="Volume")


def apply_kubernetes(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    kind = str(document.get("kind") or "Resource")
    metadata = document.get("metadata") if isinstance(document.get("metadata"), dict) else {}
    name = metadata.get("name") or document.get("name")
    namespace = metadata.get("namespace")
    aliases = []
    if name:
        aliases.extend([str(name), f"{kind}/{name}"])
        if namespace:
            aliases.append(f"{namespace}/{kind}/{name}")
    node_id = graph.add_node(
        preferred_id=clean_id(f"k8s_{relative}_{doc_index}_{kind}_{name or 'unnamed'}"),
        label=f"{kind}: {name}" if name else f"{kind}: {relative}",
        file=relative,
        name=str(name) if name else None,
        kind=kind,
        aliases=aliases,
    )

    for owner in as_list(metadata.get("ownerReferences")):
        if isinstance(owner, dict) and owner.get("name"):
            graph.add_edge(
                node_id,
                owner.get("name"),
                "ownerReference",
                stub_kind=str(owner.get("kind") or "Resource"),
            )

    spec = document.get("spec") if isinstance(document.get("spec"), dict) else {}
    template = spec.get("template") if isinstance(spec.get("template"), dict) else {}
    pod_spec = template.get("spec") if isinstance(template.get("spec"), dict) else spec
    containers = []
    if isinstance(pod_spec, dict):
        containers.extend(as_list(pod_spec.get("containers")))
        containers.extend(as_list(pod_spec.get("initContainers")))
    for container in containers:
        if not isinstance(container, dict):
            continue
        for env_from in as_list(container.get("envFrom")):
            if not isinstance(env_from, dict):
                continue
            for ref_key, relation in (("configMapRef", "configMap"), ("secretRef", "secret")):
                ref = env_from.get(ref_key)
                if isinstance(ref, dict) and ref.get("name"):
                    graph.add_edge(node_id, ref["name"], relation, stub_kind=relation.capitalize())
        for env in as_list(container.get("env")):
            if not isinstance(env, dict):
                continue
            value_from = env.get("valueFrom") if isinstance(env.get("valueFrom"), dict) else {}
            cm = value_from.get("configMapKeyRef")
            if isinstance(cm, dict) and cm.get("name"):
                graph.add_edge(node_id, cm["name"], "configMap", stub_kind="ConfigMap")
            sec = value_from.get("secretKeyRef")
            if isinstance(sec, dict) and sec.get("name"):
                graph.add_edge(node_id, sec["name"], "secret", stub_kind="Secret")
        for mount in as_list(container.get("volumeMounts")):
            if isinstance(mount, dict) and mount.get("name"):
                graph.add_edge(node_id, mount["name"], "volumeMount", stub_kind="Volume")

    annotations = metadata.get("annotations") if isinstance(metadata.get("annotations"), dict) else {}
    for key, value in annotations.items():
        if "depend" in str(key).lower():
            for dep in re.split(r"[,\s]+", str(value)):
                if dep:
                    graph.add_edge(node_id, dep, str(key), stub_kind="Resource")


def apply_github_actions(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    workflow_name = document.get("name") or Path(relative).stem
    workflow_id = graph.add_node(
        preferred_id=clean_id(f"gha_workflow_{relative}"),
        label=f"Workflow: {workflow_name}",
        file=relative,
        name=str(workflow_name),
        kind="Workflow",
        aliases=[str(workflow_name), relative, Path(relative).name],
    )
    jobs = document.get("jobs") or {}
    if not isinstance(jobs, dict):
        return
    job_ids: dict[str, str] = {}
    for job_name, job in jobs.items():
        node_id = graph.add_node(
            preferred_id=clean_id(f"gha_job_{relative}_{job_name}"),
            label=f"Job: {job_name}",
            file=relative,
            name=str(job_name),
            kind="Job",
            aliases=[str(job_name), f"job/{job_name}"],
        )
        job_ids[str(job_name)] = node_id
        graph.edges.add((workflow_id, node_id, "job"))
        if isinstance(job, dict) and job.get("uses"):
            graph.add_edge(node_id, job["uses"], "uses", stub_kind="Workflow")

    for job_name, job in jobs.items():
        if not isinstance(job, dict):
            continue
        source = job_ids[str(job_name)]
        for dep in extract_depends_targets(job.get("needs")):
            graph.add_edge(source, dep, "needs", stub_kind="Job")
        for step in as_list(job.get("steps")):
            if isinstance(step, dict) and step.get("uses"):
                graph.add_edge(source, step["uses"], "uses", stub_kind="Action")


def apply_azure_devops(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    pipeline_name = document.get("name") or Path(relative).stem
    pipeline_id = graph.add_node(
        preferred_id=clean_id(f"ado_pipeline_{relative}"),
        label=f"Pipeline: {pipeline_name}",
        file=relative,
        name=str(pipeline_name),
        kind="Pipeline",
        aliases=[str(pipeline_name)],
    )

    stages = document.get("stages")
    if isinstance(stages, list):
        for stage in stages:
            if not isinstance(stage, dict):
                continue
            stage_name = stage.get("stage") or stage.get("name")
            if not stage_name:
                continue
            stage_id = graph.add_node(
                preferred_id=clean_id(f"ado_stage_{relative}_{stage_name}"),
                label=f"Stage: {stage_name}",
                file=relative,
                name=str(stage_name),
                kind="Stage",
                aliases=[str(stage_name), f"stage/{stage_name}"],
            )
            graph.edges.add((pipeline_id, stage_id, "stage"))
            for dep in extract_depends_targets(stage.get("dependsOn")):
                graph.add_edge(stage_id, dep, "dependsOn", stub_kind="Stage")
            for job in as_list(stage.get("jobs")):
                if not isinstance(job, dict):
                    continue
                job_name = job.get("job") or job.get("name")
                if not job_name:
                    continue
                job_id = graph.add_node(
                    preferred_id=clean_id(f"ado_job_{relative}_{stage_name}_{job_name}"),
                    label=f"Job: {job_name}",
                    file=relative,
                    name=str(job_name),
                    kind="Job",
                    aliases=[str(job_name), f"job/{job_name}"],
                )
                graph.edges.add((stage_id, job_id, "job"))
                for dep in extract_depends_targets(job.get("dependsOn")):
                    graph.add_edge(job_id, dep, "dependsOn", stub_kind="Job")

    jobs = document.get("jobs")
    if isinstance(jobs, list):
        for job in jobs:
            if not isinstance(job, dict):
                continue
            job_name = job.get("job") or job.get("name")
            if not job_name:
                continue
            job_id = graph.add_node(
                preferred_id=clean_id(f"ado_job_{relative}_{job_name}"),
                label=f"Job: {job_name}",
                file=relative,
                name=str(job_name),
                kind="Job",
                aliases=[str(job_name)],
            )
            graph.edges.add((pipeline_id, job_id, "job"))
            for dep in extract_depends_targets(job.get("dependsOn")):
                graph.add_edge(job_id, dep, "dependsOn", stub_kind="Job")


def _cfn_refs(value: Any) -> list[str]:
    refs: list[str] = []
    if isinstance(value, dict):
        if "Ref" in value and scalar(value["Ref"]):
            refs.append(str(value["Ref"]))
        get_att = value.get("Fn::GetAtt") or value.get("!GetAtt")
        if isinstance(get_att, list) and get_att:
            refs.append(str(get_att[0]))
        if isinstance(get_att, str) and get_att:
            refs.append(get_att.split(".", 1)[0])
        for nested in value.values():
            refs.extend(_cfn_refs(nested))
    elif isinstance(value, list):
        for item in value:
            refs.extend(_cfn_refs(item))
    return refs


def apply_cloudformation(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    resources = document.get("Resources") or document.get("resources") or {}
    if not isinstance(resources, dict):
        return
    resource_ids: dict[str, str] = {}
    for logical_id, resource in resources.items():
        if not isinstance(resource, dict):
            continue
        rtype = resource.get("Type") or resource.get("type") or "Resource"
        node_id = graph.add_node(
            preferred_id=clean_id(f"cfn_{relative}_{logical_id}"),
            label=f"{rtype}: {logical_id}",
            file=relative,
            name=str(logical_id),
            kind=str(rtype),
            aliases=[str(logical_id), f"resource/{logical_id}"],
        )
        resource_ids[str(logical_id)] = node_id

    for logical_id, resource in resources.items():
        if not isinstance(resource, dict):
            continue
        source = resource_ids[str(logical_id)]
        for dep in extract_depends_targets(resource.get("DependsOn") or resource.get("dependsOn")):
            graph.add_edge(source, dep, "DependsOn", stub_kind="Resource")
        for ref in _cfn_refs(resource.get("Properties") or resource.get("properties") or {}):
            if ref in resource_ids and ref != logical_id:
                graph.add_edge(source, ref, "Ref", create_stub=False)


def apply_kit_manifest(graph: TopologyGraph, document: dict, relative: str, doc_index: int) -> None:
    domain = document.get("domain")
    wave = document.get("wave")
    if wave:
        node_id = graph.add_node(
            preferred_id=clean_id(f"wave_{relative}_{wave}"),
            label=f"Wave: {wave}",
            file=relative,
            name=str(wave),
            kind="Wave",
            aliases=[str(wave), f"wave/{wave}"],
        )
        if domain:
            graph.add_edge(node_id, domain, "domain", stub_kind="Domain")
        return

    if domain:
        node_id = graph.add_node(
            preferred_id=clean_id(f"domain_{relative}_{domain}"),
            label=f"Domain: {domain}",
            file=relative,
            name=str(domain),
            kind="Domain",
            aliases=[str(domain), f"domain/{domain}"],
        )
        for key, relation, stub_kind in (
            ("owner_team", "owner_team", "Team"),
            ("source_database", "source_database", "Database"),
            ("target_service", "target_service", "Service"),
            ("target_database", "target_database", "Database"),
            ("target_schema", "target_schema", "Schema"),
            ("runtime_identity", "runtime_identity", "Identity"),
            ("migration_identity", "migration_identity", "Identity"),
        ):
            value = document.get(key)
            if value:
                graph.add_edge(node_id, value, relation, stub_kind=stub_kind)
        for project in as_list(document.get("source_projects")):
            graph.add_edge(node_id, project, "source_project", stub_kind="SqlProject")
        for obj in as_list(document.get("ef_migrations_own")):
            graph.add_edge(node_id, obj, "ef_owns", stub_kind="Table")
        return

    apply_generic(graph, document, relative, doc_index)


def apply_generic(graph: TopologyGraph, document: Any, relative: str, doc_index: int) -> None:
    values = walk(document)
    kind = next((v for k, v, _ in values if k.lower() == "kind"), "YAML")
    name = next((v for k, v, _ in values if k.lower() in ID_KEYS), None)
    node_id = graph.add_node(
        preferred_id=clean_id(f"{relative}_{doc_index}_{name or kind}"),
        label=f"{kind}: {name}" if name else f"{kind}: {relative}",
        file=relative,
        name=name,
        kind=str(kind),
        aliases=[name] if name else None,
    )

    if isinstance(document, dict):
        for key in ("dependsOn", "depends_on", "dependencies", "needs", "DependsOn"):
            if key in document:
                for dep in extract_depends_targets(document.get(key)):
                    graph.add_edge(node_id, dep, key, allow_partial=True)

    for key, value, _yaml_path in values:
        lowered = key.lower()
        if lowered in REF_KEYS and value and value != name:
            if lowered in {"dependson", "depends_on", "dependencies", "needs"}:
                continue
            graph.add_edge(node_id, value, key, allow_partial=True)


ADAPTER_FUNCS = {
    "compose": apply_compose,
    "kubernetes": apply_kubernetes,
    "github-actions": apply_github_actions,
    "azure-devops": apply_azure_devops,
    "cloudformation": apply_cloudformation,
    "kit-manifest": apply_kit_manifest,
    "generic": apply_generic,
}


def build_topology(
    root: Path,
    enabled_adapters: set[str] | None = None,
    *,
    create_stubs: bool = True,
) -> tuple[list[Path], TopologyGraph]:
    enabled = set(enabled_adapters or ADAPTERS)
    files = discover_files(root)
    graph = TopologyGraph(create_stubs=create_stubs)

    for file_path in files:
        relative = str(file_path.relative_to(root)).replace("\\", "/")
        for doc_index, document in enumerate(load_documents(file_path)):
            if document is None:
                continue
            adapter = detect_adapter(document, relative)
            if adapter not in enabled:
                adapter = "generic" if "generic" in enabled else None
            if adapter is None:
                continue
            ADAPTER_FUNCS[adapter](graph, document, relative, doc_index)

    return files, graph


def render_mermaid(graph: TopologyGraph, direction: str = "LR") -> str:
    lines = [f"flowchart {direction}"]
    if not graph.nodes:
        lines.append('  empty["No YAML resources discovered"]')
        return "\n".join(lines) + "\n"

    for node_id, node in graph.nodes.items():
        label = escape_label(node["label"])
        if node.get("stub"):
            lines.append(f'  {node_id}["{label}"]:::stub')
        else:
            lines.append(f'  {node_id}["{label}"]')

    for source, destination, relation in sorted(graph.edges):
        edge_label = escape_label(relation)
        lines.append(f"  {source} -->|{edge_label}| {destination}")

    if any(node.get("stub") for node in graph.nodes.values()):
        lines.append("  classDef stub stroke-dasharray: 5 5")

    return "\n".join(lines) + "\n"


def render_dependency_table(graph: TopologyGraph) -> list[str]:
    if not graph.edges:
        return ["_No dependency edges discovered._", ""]
    lines = [
        "| From | Relation | To |",
        "|------|----------|----|",
    ]
    for source, destination, relation in sorted(graph.edges):
        src = escape_label(graph.nodes[source]["label"])
        dst = escape_label(graph.nodes[destination]["label"])
        lines.append(f"| {src} | `{relation}` | {dst} |")
    lines.append("")
    return lines


def render_markdown(
    root: Path, files: list[Path], graph: TopologyGraph, mermaid: str, title: str
) -> str:
    stubs = sum(1 for node in graph.nodes.values() if node.get("stub"))
    lines = [
        f"# {title}",
        "",
        f"Generated from `{root}`.",
        "",
        "## Summary",
        "",
        f"- YAML files scanned: **{len(files)}**",
        f"- Topology nodes: **{len(graph.nodes)}** (stubs: **{stubs}**)",
        f"- Dependency links: **{len(graph.edges)}**",
        f"- Unresolved references before stubbing: **{len(graph.unresolved)}**",
        "",
        "## Topology",
        "",
        "```mermaid",
        mermaid.rstrip(),
        "```",
        "",
        "## Dependencies",
        "",
    ]
    lines.extend(render_dependency_table(graph))
    lines.extend(
        [
            "## Notes",
            "",
            "Schema-aware adapters emit deterministic dependency edges for Docker Compose, "
            "Kubernetes, GitHub Actions, Azure DevOps, CloudFormation, and kit domain/wave manifests. "
            "Remaining references use heuristic key matching. Unresolved targets are shown as dashed stub nodes. "
            "Treat this as a repository discovery map rather than authoritative runtime state.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Recursively map YAML repositories and generate Markdown with Mermaid dependency topology."
        )
    )
    parser.add_argument("root", nargs="?", default=".", help="Repository/folder to scan recursively")
    parser.add_argument("-o", "--output", default="topology.md", help="Output .md or .mmd file")
    parser.add_argument(
        "--direction",
        choices=("LR", "RL", "TB", "BT"),
        default="LR",
        help="Mermaid flowchart direction (default: LR)",
    )
    parser.add_argument("--title", default="YAML Repository Topology", help="Markdown document title")
    parser.add_argument(
        "--format",
        choices=("auto", "markdown", "mermaid"),
        default="auto",
        help="Output format. auto uses .mmd => Mermaid, otherwise Markdown.",
    )
    parser.add_argument(
        "--adapters",
        default=",".join(ADAPTERS),
        help=f"Comma-separated adapters to enable. Available: {', '.join(ADAPTERS)}",
    )
    parser.add_argument(
        "--no-stubs",
        action="store_true",
        help="Do not create stub nodes for unresolved dependency targets",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists() or not root.is_dir():
        raise SystemExit(f"Repository folder not found: {root}")

    enabled = {item.strip() for item in args.adapters.split(",") if item.strip()}
    unknown = enabled - set(ADAPTERS)
    if unknown:
        raise SystemExit(f"Unknown adapters: {', '.join(sorted(unknown))}")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)

    files, graph = build_topology(root, enabled, create_stubs=not args.no_stubs)
    mermaid = render_mermaid(graph, args.direction)

    output_format = args.format
    if output_format == "auto":
        output_format = "mermaid" if output.suffix.lower() == ".mmd" else "markdown"

    if output_format == "mermaid":
        content = mermaid
    else:
        content = render_markdown(root, files, graph, mermaid, args.title)

    output.write_text(content, encoding="utf-8")

    print(
        f"YAML files: {len(files)} | nodes: {len(graph.nodes)} | "
        f"edges: {len(graph.edges)} | unresolved: {len(graph.unresolved)}"
    )
    print(f"Wrote {output_format}: {output.resolve()}")


if __name__ == "__main__":
    main()
