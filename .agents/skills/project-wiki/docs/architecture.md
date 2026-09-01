# Architecture

## Goal

Turn a project's source tree into a continuously updatable local wiki
website, built as a knowledge system rather than a plain code documentation
generator:

```text
Project Source
    ↓
Static Analysis
    ↓
Knowledge Graph
    ↓
Wiki Documents
    ↓
Cross-Link / Caption Processing
    ↓
Local Wiki Website
```

## Three-layer responsibility split

This project is intentionally **not** an LLM-only system. Responsibilities
are split into three layers:

| Layer | Responsibility |
|---|---|
| Deterministic Tools (the `project-wiki` engine/CLI) | Fact extraction and verification |
| Agent Skill (this skill) | Semantic judgement and document authoring |
| Site Renderer | Presentation |

Concretely:

| Area | Owner |
|---|---|
| File discovery | Code |
| Git diff | Code |
| Hashing | Code |
| C# AST | Code |
| Unity GUID | Code |
| Relation storage | Code |
| Broken link checking | Code |
| Backlink generation | Code |
| System-level semantic judgement | Agent |
| Feature classification | Agent |
| Document prose | Agent |
| Cross-link context resolution | Agent |
| Caption prose | Agent |

## Engine / Skill separation

The `project-wiki` engine is a standalone CLI that must work without any AI
agent attached. The Agent Skill orchestrates the engine and adds the
semantic/authoring steps the engine intentionally does not perform.

```text
project-wiki engine
       ↑
       │
 Agent Skill
```

Never the reverse (engine logic must not live inside the agent skill
prompts).

## Repository layout

```text
/
├─ AGENTS.md
├─ .agents/skills/project-wiki/
│  ├─ SKILL.md
│  ├─ docs/
│  ├─ scripts/        (points at the project-wiki CLI/engine source)
│  ├─ schemas/
│  └─ templates/
├─ .github/copilot-instructions.md
├─ src/                (project-wiki engine + CLI source, .NET)
├─ tests/              (unit + integration tests, fixtures)
```

## Milestones

Implementation proceeds in milestones so that the data model stays solid
as functionality grows:

- **M1 - Core Engine**: CLI, config, project scanner, knowledge schemas,
  Roslyn analyzer, entity/relation model, tests. *(implemented)*
- **M2 - Wiki Documents**: document planner, Markdown storage, AUTO block
  handling, templates, human-content preservation.
- **M3 - Wiki Navigation**: deterministic alias, redirect, Markdown wiki-link,
  backlink, and navigation-validation core. CLI wiring, captions, and
  agent-authored cross-link insertion remain later work.
- **M4 - Incremental Update**: git tracking, hash tracking, rename
  detection, impact analysis, incremental document update, recent changes.
- **M5 - Unity**: GUID, scene, prefab, asset, asmdef, and package analysis.
  *(implemented)*
- **M6 - Web UI**: site generator, search, TOC, caption popup, backlinks,
  health, serve.
