# Private-Wiki
개인 위키 작성용 에이전트 스킬

## Project Wiki Agent Skill

This repository implements a reusable, agent-portable "Project Wiki" skill:
a deterministic engine that scans a source project and builds a
continuously updatable knowledge graph, paired with an Agent Skill that
orchestrates it. See:

- [`AGENTS.md`](AGENTS.md) — entry point for any coding agent
- [`.agents/skills/project-wiki/SKILL.md`](.agents/skills/project-wiki/SKILL.md) — skill workflow controller
- [`.agents/skills/project-wiki/docs/architecture.md`](.agents/skills/project-wiki/docs/architecture.md) — overall architecture and milestone plan

### Engine (Milestones 4–5 incremental updates and Unity analysis)

The `project-wiki` engine/CLI (`src/ProjectWiki.Core`, `src/ProjectWiki.Cli`)
implements the deterministic core: project scanning, file hashing, git
detection, Roslyn-based C# static analysis, and initial architecture document
generation. It works standalone, without any AI agent attached. Documents use
delimited `AUTO` blocks so generated sections can be refreshed without
overwriting human-authored content. The engine also persists aliases and
redirects, validates Markdown wiki links, atomically rebuilds backlinks, and
supports hash-based `update`, `rebuild`, and graph-context `inspect` workflows.
For detected Unity projects, it also deterministically indexes `.meta` GUIDs,
serialized GUID references, assembly definitions, and manifest packages.

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectWiki.Cli -- init --project /path/to/project --wiki /path/to/wiki
dotnet run --project src/ProjectWiki.Cli -- update --wiki /path/to/wiki
dotnet run --project src/ProjectWiki.Cli -- inspect entity-id --wiki /path/to/wiki
dotnet run --project src/ProjectWiki.Cli -- build --wiki /path/to/wiki
dotnet run --project src/ProjectWiki.Cli -- serve --wiki /path/to/wiki --port 8080
```

`build` creates a static site in `<wiki>/site`; `serve` binds it only to
`127.0.0.1`.
