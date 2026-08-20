#!/usr/bin/env python3
"""Recursive YAML repository topology mapper.

Reads *.yaml / *.yml files recursively and writes either:
- Markdown with a MermaidJS diagram (default for .md output), or
- raw Mermaid source (for .mmd output).

No Graphviz, Go, Docker, or administrator rights are required.
"""

import argparse
import re
from pathlib import Path

try:
    import yaml
except ImportError:
    raise SystemExit("Missing PyYAML. Run: python -m pip install pyyaml")

ID_KEYS = {"name", "id", "service", "app", "application", "component"}
REF_KEYS = {
    "dependson", "depends_on", "ref", "reference", "target", "service",
    "database", "queue", "topic", "cluster", "host", "endpoint",
    "backend", "upstream"
}
SKIP = {".git", ".venv", "venv", "node_modules", "bin", "obj", ".terraform"}


def scalar(value):
    return isinstance(value, (str, int, float, bool))


def clean_id(value):
    """Create a Mermaid-safe node id."""
    cleaned = re.sub(r"[^A-Za-z0-9_]+", "_", str(value)).strip("_")
    if not cleaned:
        cleaned = "node"
    if cleaned[0].isdigit():
        cleaned = "n_" + cleaned
    return cleaned[:180]


def escape_label(value):
    return str(value).replace("\\", "\\\\").replace('"', "'").replace("\n", " ")


def walk(obj, path="", found=None):
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


def load_documents(path):
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            return list(yaml.safe_load_all(handle))
    except Exception as exc:
        print(f"WARN {path}: {exc}")
        return []


def discover_files(root):
    return sorted(
        p for p in root.rglob("*")
        if p.is_file()
        and p.suffix.lower() in (".yaml", ".yml")
        and not any(part in SKIP for part in p.parts)
    )


def build_topology(root):
    files = discover_files(root)
    nodes = {}
    refs = []
    used_ids = set()

    for file_path in files:
        relative = str(file_path.relative_to(root)).replace("\\", "/")
        for doc_index, document in enumerate(load_documents(file_path)):
            if document is None:
                continue

            values = walk(document)
            kind = next((v for k, v, _ in values if k.lower() == "kind"), "YAML")
            name = next((v for k, v, _ in values if k.lower() in ID_KEYS), None)

            base_id = clean_id(f"{relative}_{doc_index}_{name or kind}")
            node_id = base_id
            suffix = 2
            while node_id in used_ids:
                node_id = f"{base_id}_{suffix}"
                suffix += 1
            used_ids.add(node_id)

            label = f"{kind}: {name}" if name else f"{kind}: {relative}"
            nodes[node_id] = {
                "label": label,
                "file": relative,
                "name": name,
                "kind": kind,
            }

            for key, value, yaml_path in values:
                if key.lower() in REF_KEYS and value and value != name:
                    refs.append((node_id, key, value, yaml_path))

    # Resolve references heuristically against resource names first, then labels.
    edges = set()
    for source, key, value, _yaml_path in refs:
        needle = str(value).strip().lower()
        if not needle:
            continue

        exact = [
            node_id for node_id, node in nodes.items()
            if node_id != source and node.get("name") and needle == str(node["name"]).lower()
        ]
        matches = exact or [
            node_id for node_id, node in nodes.items()
            if node_id != source and needle in str(node["label"]).lower()
        ]
        for destination in matches[:5]:
            edges.add((source, destination, key))

    return files, nodes, edges


def render_mermaid(nodes, edges, direction="LR"):
    lines = [f"flowchart {direction}"]
    if not nodes:
        lines.append('  empty["No YAML resources discovered"]')
        return "\n".join(lines) + "\n"

    for node_id, node in nodes.items():
        label = escape_label(node["label"])
        lines.append(f'  {node_id}["{label}"]')

    for source, destination, relation in sorted(edges):
        edge_label = escape_label(relation)
        lines.append(f"  {source} -->|{edge_label}| {destination}")

    return "\n".join(lines) + "\n"


def render_markdown(root, files, nodes, edges, mermaid, title):
    lines = [
        f"# {title}",
        "",
        f"Generated from `{root}`.",
        "",
        "## Summary",
        "",
        f"- YAML files scanned: **{len(files)}**",
        f"- Topology nodes: **{len(nodes)}**",
        f"- Inferred relationships: **{len(edges)}**",
        "",
        "## Topology",
        "",
        "```mermaid",
        mermaid.rstrip(),
        "```",
        "",
        "## Notes",
        "",
        "Relationships are inferred heuristically from common YAML identity and reference keys. "
        "Treat this as a repository discovery map rather than authoritative deployment state.",
        "",
    ]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(
        description="Recursively map a YAML repository and generate Markdown with a MermaidJS topology."
    )
    parser.add_argument("root", nargs="?", default=".", help="Repository/folder to scan recursively")
    parser.add_argument("-o", "--output", default="topology.md", help="Output .md or .mmd file")
    parser.add_argument(
        "--direction", choices=("LR", "RL", "TB", "BT"), default="LR",
        help="Mermaid flowchart direction (default: LR)"
    )
    parser.add_argument("--title", default="YAML Repository Topology", help="Markdown document title")
    parser.add_argument(
        "--format", choices=("auto", "markdown", "mermaid"), default="auto",
        help="Output format. auto uses .mmd => Mermaid, otherwise Markdown."
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists() or not root.is_dir():
        raise SystemExit(f"Repository folder not found: {root}")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)

    files, nodes, edges = build_topology(root)
    mermaid = render_mermaid(nodes, edges, args.direction)

    output_format = args.format
    if output_format == "auto":
        output_format = "mermaid" if output.suffix.lower() == ".mmd" else "markdown"

    if output_format == "mermaid":
        content = mermaid
    else:
        content = render_markdown(root, files, nodes, edges, mermaid, args.title)

    output.write_text(content, encoding="utf-8")

    print(f"YAML files: {len(files)} | nodes: {len(nodes)} | edges: {len(edges)}")
    print(f"Wrote {output_format}: {output.resolve()}")


if __name__ == "__main__":
    main()
