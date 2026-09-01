# Copilot Repository Instructions

This repository implements reusable Agent Skills.

For project wiki related tasks, follow the project-wiki skill at:

.agents/skills/project-wiki/SKILL.md

Treat AGENTS.md and the skill documentation as authoritative.

When implementing or modifying the skill:

- execute relevant tests,
- avoid replacing deterministic analysis with LLM guesses,
- preserve agent portability,
- do not introduce Copilot-only dependencies into the skill core.
