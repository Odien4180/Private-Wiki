# Validation (Milestone 3 Navigation Core)

`WikiEngine.ValidateNavigation` performs read-only, deterministic validation
of persisted navigation data and Markdown documents. It reports:

- duplicate entity ids, aliases, and redirects;
- empty aliases or redirects, aliases targeting missing entities, and redirects
  that are broken, ambiguous, shadow an entity id, or cycle;
- malformed, broken, and ambiguous `[[wiki links]]`;
- a missing, stale, duplicated, or otherwise invalid backlink entry.

`WikiEngine.BuildNavigation` rebuilds `knowledge/backlinks.json` from the
current Markdown documents using atomic replacement. It preserves existing
aliases and redirects. CLI commands for these engine methods are deferred to
a later milestone.
