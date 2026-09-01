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

### Engine (Milestone 3 navigation core)

The `project-wiki` engine/CLI (`src/ProjectWiki.Core`, `src/ProjectWiki.Cli`)
implements the deterministic core: project scanning, file hashing, git
detection, Roslyn-based C# static analysis, and initial architecture document
generation. It works standalone, without any AI agent attached. Documents use
delimited `AUTO` blocks so generated sections can be refreshed without
overwriting human-authored content. The engine also persists aliases and
redirects, validates Markdown wiki links, and atomically rebuilds backlinks.

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectWiki.Cli -- init --project /path/to/project --wiki /path/to/wiki
```

Later milestones (document generation, cross-linking, incremental update,
Unity analysis, web UI) are tracked in
[`.agents/skills/project-wiki/docs/architecture.md`](.agents/skills/project-wiki/docs/architecture.md).
