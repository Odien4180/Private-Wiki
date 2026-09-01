# Unity Analysis (Milestone 5)

Unity analysis runs only when the project has both `Assets/` and
`ProjectSettings/` directories. It is performed during `init`, `update`, and
`rebuild`; non-Unity projects receive no Unity graph records.

## Deterministic facts

- A `.meta` file with exactly one `guid: <32 hexadecimal characters>` entry
  creates an asset-backed entity. The entity carries the exact GUID in
  `symbols`, includes the `.meta` path in `sources`, and is typed as `scene`,
  `prefab`, `data`, `config` (for `.asmdef`), or `asset` from its extension.
- Textual `guid:` mapping values in `.unity`, `.prefab`, and `.asset` files
  create high-confidence `references` relations only when both source and
  target have entities and the target GUID is unique. Each relation records
  the source path and exact serialized line.
- Valid `.asmdef` JSON contributes an assembly configuration entity. Its exact
  `name` and string `references` are stored in the entity, and an unambiguous
  assembly-name or `GUID:<32 hexadecimal characters>` reference creates a
  high-confidence `depends_on` relation with file evidence.
- String entries in `Packages/manifest.json` `dependencies` create `package`
  entities. Their package names and exact declared versions are retained in
  the entity and sourced to the manifest.

All Unity facts use the existing `knowledge/entities.json` and
`knowledge/relations.json` formats. Invalid JSON, missing metadata, unknown
GUIDs, duplicate GUIDs, and non-text/binary serialized content create no
guessed relation or package/assembly fact.
