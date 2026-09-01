# Incremental Update (Milestone 4)

`project-wiki update --wiki <path>` reindexes the project recorded in
`wiki.config.json`. It compares `tracking/hashes.json` with a fresh project
scan; timestamps and git status are never used to classify a source change.
Git facts remain informational in `tracking/git.json`.

Each `tracking/updates.json` entry is typed and records `added`, `modified`,
`deleted`, or `renamed` files. A rename is emitted only where exactly one
removed and one added path share a SHA-256 value. Duplicate-content groups
remain independent additions and deletions rather than guessed renames.

Impact starts with entities whose source path changed (including an old path
for deletions and renames), then traverses both old and new relation graphs in
both directions. The update record distinguishes direct entity ids, related
entity ids, all affected entity ids, and the number of affected relations.

The engine performs a complete deterministic C# graph extraction during each
update to correctly account for deleted symbols and structural relations.
It retains non-analyzer graph records, aliases still targeting current
entities, redirects, and all manual Markdown content. Only known `AUTO`
blocks are replaced. Every persisted JSON and Markdown output uses atomic
replacement. `rebuild` follows the same preservation rules and records an
explicit rebuild entry; `inspect <entity>` resolves an id, alias, or redirect
and returns local graph and backlink context.
