# Update Workflow

Use this reference for existing wikis after `project-wiki update --wiki <wiki>`.

## Incremental sequence

1. Read the latest `tracking/updates.json` entry.
2. Use direct and related impact entity IDs to locate affected docs.
3. Query only affected entities with `inspect --depth 2` or `context --limit 50`.
4. Refresh only impacted `AGENT` blocks.
5. Preserve manual content and unchanged documents.
6. Run `project-wiki build` or `project-wiki navigation build` before validation.
7. Run `project-wiki validate --require-documents`.
8. Write `reports/recent-changes.md` when authoring changes were made.

## Deletions and orphans

If a deleted entity is still linked, validation should surface `stale_agent_document`. Warn rather than silently removing user-owned prose. Report orphan documents when no current entity, system, feature, or source path supports them.

## Stop conditions

Stop and ask for guidance if update impact is project-wide, if renamed/deleted entities cannot be mapped safely, or if preserving manual notes conflicts with regenerated source evidence.
