# Analysis Rules

## Project Scanner

The scanner walks `project_root` and collects, per file:

```text
path
extension
size
mtime
hash (SHA-256)
git status (if the project is a git repository)
file category
```

Default exclusions (glob patterns, case-insensitive):

```text
.git/**
Library/**
Temp/**
Logs/**
obj/**
bin/**
node_modules/**
Build/**
Builds/**
```

Additional exclusions can be supplied via `wiki.config.json` `exclude`.

## Static Analyzer (C#)

C# source is analyzed with the Roslyn compiler APIs (
`Microsoft.CodeAnalysis.CSharp`), **not** a regular-expression parser.
Regex may only be used for cheap pre-filtering (e.g. "does this file
contain `class`"), never as the source of truth for structure.

Minimum extraction, per file:

```text
Namespace
Class
Struct
Interface
Enum
Method
Property
Field
Inheritance
Interface implementation
Type references
Attributes
```

Each extracted symbol becomes (or contributes to) an Entity, and each
structural relationship (inheritance, interface implementation, type
reference) becomes a Relation with `confidence: high` and an `evidence`
entry pointing at the originating file (and, when the position is known,
line span).

## Confidence

```text
high    - AST direct reference, Unity GUID, explicit configuration
medium  - semantic agent inference
low     - name-only inference
```

Low-confidence relationships must never be presented as confirmed facts in
generated documents.

## Non-goals for the deterministic engine

The engine never:

- guesses semantic meaning of a system/feature (that is an Agent task),
- invents source locations or line numbers it cannot compute,
- infers a relationship from symbol names alone (that would be `low`
  confidence surfaced explicitly, not silently upgraded).
