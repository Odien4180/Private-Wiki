# Linking Rules (Milestone 3 Navigation Core)

The deterministic navigation core recognizes these Markdown wiki links:

- `[[Entity]]`
- `[[Entity|Display Name]]`

`Entity` is resolved case-insensitively after trimming whitespace. Entity ids
take precedence, followed by redirects and aliases. The engine does not insert
links automatically: link density, semantic context, and prose remain agent
responsibilities.

The parser ignores escaped links, inline-code spans, and fenced code blocks.
Only uniquely resolved links become backlinks; ambiguous and broken links are
reported by validation.

`knowledge/aliases.json` stores alias-to-entity-id entries. A single alias may
intentionally name multiple entities, but a link using it is invalid until it
is made unambiguous. `knowledge/redirects.json` stores old-name-to-target
entries and may chain, but may not cycle.
