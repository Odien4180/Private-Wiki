# Analysis Rules

## Project Scanner

The scanner walks `project_root` and collects path, extension, size, modification time, SHA-256 hash, git status when available, and file category.

Default exclusions are case-insensitive glob patterns:

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

Unity projects also use the Unity exclusion profile:

```text
Assets/AmplifyShaderEditor/**
Assets/AmplifyShaderPack/**
Assets/NiloToonURP/**
Assets/Packages/**
Assets/TextMesh Pro/**
```

`Assets/Plugins/**` is only a review candidate because project code may be mixed into that folder.

Use `project-wiki scope --project <path>` before authoring to preview included files, excluded files, Unity third-party noise, and review candidates. Use `project-wiki init --exclude <glob>` and `--include <glob>` to refine scope. The effective scope is persisted to `reports/analysis-scope.json`.

## Static Analyzer (C#)

C# source is analyzed with Roslyn compiler APIs, not regular expressions. Regex may only be used for cheap pre-filtering, never as the source of truth for structure.

Minimum extraction includes namespace, class, struct, interface, enum, method, property, field, inheritance, interface implementation, type references, and attributes.

Each extracted symbol becomes or contributes to an Entity. Structural relationships become Relations with evidence pointing at a real source file and line span when known.

## Confidence

```text
high    - AST direct reference, Unity GUID, explicit configuration
medium  - semantic agent inference
low     - name-only inference
```

Low-confidence relationships must never be presented as confirmed facts.

## Non-goals

The deterministic engine never guesses semantic system/feature meaning, invents source locations, or silently upgrades name-only inference.
