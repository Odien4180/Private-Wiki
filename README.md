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

## One-touch agent and CLI installation (Windows)

`tools/Install-ProjectWiki.ps1` copies the complete skill directory and
packages/registers the `project-wiki` CLI as a global .NET tool. It backs up a
previously installed `project-wiki` skill instead of deleting it.

For a double-clickable Windows launcher, use `tools/Install-ProjectWiki.cmd`.
It forwards any target-selection arguments to the PowerShell installer.

```powershell
# Codex (default)
.\tools\Install-ProjectWiki.ps1

# Or run the launcher without changing the local execution-policy setting
.\tools\Install-ProjectWiki.cmd -Target Codex

# One selected agent runtime
.\tools\Install-ProjectWiki.ps1 -Target Claude
.\tools\Install-ProjectWiki.ps1 -Target Copilot

# All supported personal skill locations
.\tools\Install-ProjectWiki.ps1 -Target All

# Any compatible skill root
.\tools\Install-ProjectWiki.ps1 -Target Custom -Destination D:\AgentSkills
```

The default target roots are `%USERPROFILE%\.codex\skills`,
`%USERPROFILE%\.claude\skills`, and `%USERPROFILE%\.copilot\skills`.
Use `-SkipCli` when only the skill files should be installed. Start a new agent
session after installation; then verify the command with `project-wiki --help`.
