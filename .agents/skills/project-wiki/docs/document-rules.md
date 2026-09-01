# Document Rules

Project Wiki documents have three ownership zones:

```markdown
<!-- AUTO:SUMMARY:START -->
Deterministic engine output.
<!-- AUTO:SUMMARY:END -->

<!-- AGENT:EXPLANATION:START -->
Agent-authored explanation grounded in CLI evidence.
<!-- AGENT:EXPLANATION:END -->

## Developer Notes

User-owned notes. Do not rewrite automatically.
```

## Ownership

- `AUTO` blocks are owned only by the deterministic CLI.
- `AGENT` blocks may be created and refreshed by the agent skill.
- Content outside `AUTO` and `AGENT` blocks is user-owned and must be preserved.

## Required document areas

Authoring should cover:

- `documents/project/`
- `documents/architecture/`
- `documents/systems/`
- `documents/features/`
- `documents/classes/`
- `documents/scenes/`
- `documents/data/`
- `documents/packages/`

System documents are not complete when they only contain entity counts or class lists. Each system document should describe purpose and responsibility, entry points, core classes, execution flow, state/data flow, external dependencies, Scene/Prefab relationships, related features, source evidence, wiki links, and uncertain inference.

## Planning inputs

Use `knowledge/document-plan.json` as candidate evidence only. The engine may group by folder, namespace, assembly, scene entry point, component usage, relation centrality, and package boundary, but final system and feature meaning is the agent's responsibility.
