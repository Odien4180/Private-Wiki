# Scripts

The deterministic `project-wiki` engine/CLI is implemented as a standalone
.NET solution at the repository root (not duplicated here), so that it can
be built, tested and reused independently of any agent:

```text
src/ProjectWiki.Core/   # engine: config, scanner, hashing, git detection,
                         # Roslyn-based C# analysis, entity/relation model,
                         # JSON persistence
src/ProjectWiki.Cli/    # cross-platform CLI (`project-wiki`)
tests/                   # unit + integration tests, fixtures
```

## Build & test

```bash
dotnet build
dotnet test
```

## Commands (current status)

| Command | Status |
|---|---|
| `project-wiki scope --project <path> [--include <glob>] [--exclude <glob>]` | Implemented |
| `project-wiki init --project <path> --wiki <path> [--title <title>] [--language <lang>] [--include <glob>] [--exclude <glob>]` | Implemented (Milestones 1 and 5 plus scope controls) |
| `project-wiki update --wiki <path>` | Implemented (Milestones 4 and 5) |
| `project-wiki rebuild --wiki <path>` | Implemented (Milestones 4 and 5) |
| `project-wiki list --wiki <path> [--type <type>] [--source <glob>]` | Implemented |
| `project-wiki inspect <entity> --wiki <path> [--depth <n>]` | Implemented (Milestone 4 plus depth) |
| `project-wiki context --wiki <path> [--topic <text>] [--source <glob>] [--depth <n>]` | Implemented |
| `project-wiki validate --wiki <path> [--require-documents] [--min-coverage <0..1>]` | Implemented (navigation plus quality gates) |
| `project-wiki build --wiki <path>` | Implemented (Milestone 6) |
| `project-wiki serve --wiki <path> [--port <1-65535>]` | Implemented (Milestone 6) |

`scope` previews the effective analysis scope, including default exclusions,
Unity vendor exclusions, user include/exclude patterns, and review candidates.
`init` scans the project, runs the C# static analyzer, and, only for detected
Unity projects, extracts `.meta` GUID mappings, serialized scene/prefab/asset
GUID references, assembly definition references, and manifest package facts.
It builds the initial knowledge graph (entities + relations with evidence),
persists it under `wiki_root`, writes `reports/analysis-scope.json` and
`knowledge/document-plan.json`, and generates the initial architecture document.
It initializes typed aliases and redirects and atomically writes an empty
backlink index.
The public `WikiEngine.BuildNavigation` and
`WikiEngine.ValidateNavigation` methods provide the Milestone 3 deterministic
navigation core; corresponding CLI commands are not wired yet.

`update` compares the persisted and current SHA-256 source snapshots to
deterministically report additions, modifications, deletions, and unambiguous
renames. It reindexes the graph, calculates relation-graph impact, appends a
typed entry to `tracking/updates.json`, and only refreshes document `AUTO`
blocks. `rebuild` performs the same full reindex explicitly. `inspect` resolves
an entity id, alias, or redirect and returns the entity, adjacent relations,
and backlinks as JSON. Unity projects rerun the same Unity analyzer on update
and rebuild; other project types run no Unity analysis. `list`, `inspect`, and
`context` provide filtered access to large knowledge graphs so agents do not
need to read large `entities.json` and `relations.json` files directly.

`build` deterministically renders Markdown documents into `<wiki>/site`,
including a sidebar, per-page table of contents, resolved wiki links,
backlinks, source captions from `knowledge/captions.json`, a client-side
`search-index.json`, and `reports/site-health.json`. `serve` first performs
the same build, then exposes only generated files over `127.0.0.1`; it rejects
path traversal requests and non-loopback binding.

Example:

```bash
dotnet run --project src/ProjectWiki.Cli -- init --project /path/to/project --wiki /path/to/wiki
```
