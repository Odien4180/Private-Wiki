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
- scope
- inspect
- validate
- rebuild
- build
- serve

> Implementation status: all commands are implemented through Milestone 6:
> `scope`, `init`, `update`, `rebuild`, `list`, `inspect`, `context`,
> `navigation build`, `validate`, `build`, and `serve`.

## Workflow

Before first initialization:
read docs/architecture.md
read docs/analysis-rules.md
read references/scope-selection.md

For Unity projects:
read docs/unity-analysis.md

Before document generation:
read docs/document-rules.md
read references/authoring-workflow.md

Before cross-link processing:
read docs/linking-rules.md
read docs/caption-rules.md

For an existing wiki:
read docs/incremental-update.md
read references/update-workflow.md

Before completing:
read docs/validation.md
read references/quality-gates.md

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
→ build or navigation build to regenerate backlinks
→ validate --require-documents
→ fix, build, and validate again as needed
→ final build
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
project-wiki scope --project <path> --summary
project-wiki list --wiki <path> --type class --limit 100
project-wiki context --wiki <path> --topic authentication --limit 50
project-wiki navigation build --wiki <path>
project-wiki validate --wiki <path> --require-documents --min-coverage 0.7
```

See `scripts/README.md` for the full command list and current
implementation status.

## Installation and fallback

Install the reusable skill into the user's Codex skills directory:

```bash
mkdir -p ~/.codex/skills
cp -R <skill-repo>/.agents/skills/project-wiki ~/.codex/skills/project-wiki
```

On Windows, copy the folder to:

```text
C:\Users\<User>\.codex\skills\project-wiki
```

When `project-wiki` is not available on `PATH`, first try:

```bash
dotnet tool install --global ProjectWiki.Cli
```

For local development of this repository, use:

```bash
dotnet run --project <skill-repo>/src/ProjectWiki.Cli -- <command>
```

If neither the global tool nor the local source checkout is available, stop and
tell the user how to install the skill and CLI instead of guessing analysis
facts.

After installation, start a fresh Codex session and verify that `$project-wiki`
is discoverable before using it in another repository.
