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

> Implementation status: all commands are implemented through Milestone 6:
> `init`, `update`, `inspect`, `validate`, `rebuild`, `build`, and `serve`.

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

For authoring tasks, do not stop after `init` or `update`. The normal flow is:

```text
init or update
→ review analysis scope
→ review knowledge/document-plan.json
→ classify systems and features
→ query related sources/entities
→ write Markdown AGENT blocks
→ insert wiki links
→ write source captions from evidence
→ validate
→ build
```

Write authored documentation under `documents/project/`,
`documents/architecture/`, `documents/systems/`, `documents/features/`,
`documents/classes/`, `documents/scenes/`, `documents/data/`, and
`documents/packages/` as appropriate. Preserve content outside `AUTO` and
`AGENT` blocks.

## CLI

This skill orchestrates the `project-wiki` CLI (see `scripts/`). The CLI
is a standalone .NET tool that works without any AI agent; the skill only
decides *when* to call it and what to do with agent-only steps (semantic
classification, prose generation, cross-link context resolution, captions).

```bash
project-wiki init --project <path> --wiki <path>
project-wiki scope --project <path>
project-wiki list --wiki <path> --type class
project-wiki context --wiki <path> --topic authentication
project-wiki validate --wiki <path> --require-documents --min-coverage 0.7
```

See `scripts/README.md` for the full command list and current
implementation status.
