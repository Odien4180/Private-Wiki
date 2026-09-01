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
| `project-wiki init --project <path> --wiki <path> [--title <title>] [--language <lang>]` | Implemented (Milestone 1: deterministic core only) |
| `project-wiki update --wiki <path>` | Planned (Milestone 4) |
| `project-wiki inspect <entity> --wiki <path>` | Planned |
| `project-wiki validate --wiki <path>` | CLI wiring planned; the Milestone 3 engine API is implemented |
| `project-wiki build --wiki <path>` | Planned (Milestone 6) |
| `project-wiki serve --wiki <path>` | Planned (Milestone 6) |

`init` scans the project, runs the C# static analyzer, builds the initial
knowledge graph (entities + relations with evidence), persists it under
`wiki_root`, and generates the initial architecture document. It initializes
typed aliases and redirects and atomically writes an empty backlink index.
The public `WikiEngine.BuildNavigation` and
`WikiEngine.ValidateNavigation` methods provide the Milestone 3 deterministic
navigation core; corresponding CLI commands are not wired yet.

Example:

```bash
dotnet run --project src/ProjectWiki.Cli -- init --project /path/to/project --wiki /path/to/wiki
```
