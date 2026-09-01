---
name: project-wiki
description: >
  Analyze a software project and create or maintain a persistent
  local wiki with project architecture, systems, features,
  cross-links, captions, backlinks and source traceability.
---

# Project Wiki

Use this skill when asked to:

- create a project wiki
- update an existing project wiki
- document a codebase
- inspect a project subsystem
- build a local codebase knowledge base

## Inputs

Required:

- project_root
- wiki_root

## Modes

- init
- update
- inspect
- validate
- rebuild
- serve

> Implementation status: `init` (Milestone 1, deterministic core only) is
> implemented. `update`, `inspect`, `validate`, `rebuild` and `serve` are
> planned for later milestones (see `docs/` for the milestone breakdown).

## Workflow

Before first initialization:
read docs/architecture.md
read docs/analysis-rules.md

For Unity projects:
read docs/unity-analysis.md

Before document generation:
read docs/document-rules.md

Before cross-link processing:
read docs/linking-rules.md
read docs/caption-rules.md

For an existing wiki:
read docs/incremental-update.md

Before completing:
read docs/validation.md

Never infer deterministic source facts that can be obtained
from the supplied analysis tools.

## CLI

This skill orchestrates the `project-wiki` CLI (see `scripts/`). The CLI
is a standalone .NET tool that works without any AI agent; the skill only
decides *when* to call it and what to do with agent-only steps (semantic
classification, prose generation, cross-link context resolution, captions).

```bash
project-wiki init --project <path> --wiki <path>
```

See `scripts/README.md` for the full command list and current
implementation status.
