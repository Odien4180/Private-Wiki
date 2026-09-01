# Agent Instructions

This repository contains reusable agent skills.

When the user asks to create, initialize, update, inspect,
validate, rebuild, or serve a project wiki, use the
`project-wiki` skill located at:

.agents/skills/project-wiki/

Read its SKILL.md before performing the task.

Do not duplicate project-wiki logic directly in AGENTS.md.

All deterministic project analysis must be performed using
the tools provided by the skill (the `project-wiki` CLI under
`.agents/skills/project-wiki/scripts/`) rather than guessed by
the model.
