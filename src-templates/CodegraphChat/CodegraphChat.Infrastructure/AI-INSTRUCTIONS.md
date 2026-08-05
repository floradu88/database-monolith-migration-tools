# AI Instructions — `CodegraphChat.Infrastructure`

## Purpose

Codegraph CLI client, intent routing, and topic answer formatting.

## Rules

1. Prefer Codegraph CLI JSON (`--json`) when available; degrade gracefully on schema drift.
2. Reuse the Windows cmd-shim pattern from DbIntelligence `CliProcessRunner` for `codegraph` on PATH.
3. Do not call external LLM APIs; answers are Codegraph evidence only.
