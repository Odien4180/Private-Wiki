# Incremental Update

`project-wiki update --wiki <path>` reindexes the configured project, records file changes in `tracking/updates.json`, refreshes deterministic graph data, updates `reports/analysis-scope.json`, and preserves user-owned content.

Agent update workflow:

1. Read the latest `tracking/updates.json` entry.
2. Identify affected entities from direct and related impact IDs.
3. Find related system, feature, class, scene, data, and package documents.
4. Re-check only affected `AGENT` blocks.
5. Preserve manual content and unchanged documents.
6. Warn when documents mention deleted entities.
7. Report orphan documents and write recent changes to `reports/recent-changes.md` when authoring updates.
8. Rebuild links, captions, backlinks, validation, and site output.

Never rewrite every document after a small source change unless impact analysis shows project-wide effects.
