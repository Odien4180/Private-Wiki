# Authoring Workflow

Use this reference after `init` or `update` when the task asks for a complete wiki, system documentation, feature documentation, or source-grounded project explanation.

## Required sequence

1. Run `project-wiki scope --project <project> --summary` or inspect `reports/analysis-scope.json`.
2. Review `knowledge/document-plan.json` without treating candidates as final meaning.
3. Select a small batch of candidate systems/features/classes.
4. Query evidence with `project-wiki list`, `project-wiki inspect`, and `project-wiki context` using limits.
5. Create or update Markdown documents under `documents/`.
6. Write only inside `AGENT` blocks unless creating a new document from a template.
7. Insert unambiguous `[[Entity]]` links on the first meaningful prose occurrence only.
8. Add source captions using exact evidence paths and known line numbers only.
9. Run `project-wiki build --wiki <wiki>` or `project-wiki navigation build --wiki <wiki>` to regenerate backlinks.
10. Run `project-wiki validate --wiki <wiki> --require-documents` and fix failures.
11. Repeat build/validate until clean, then run `project-wiki build --wiki <wiki>` for final site output.

## Document-plan selection

Prefer candidates that are first-party, central in the relation graph, scene or prefab entry points, or clusters with clear namespace/folder cohesion. Treat external package candidates as boundary/dependency documents, not as project systems. Do not promote a candidate whose only evidence is a noisy external asset path.

## Batch processing

For large projects, work in batches. Start with architecture and 3-5 high-centrality systems, then features, then important classes/scenes/data/packages. Use `--limit 50` for context queries and `list --limit 100 --offset <n>` for pagination. Do not read large `entities.json` or `relations.json` files directly unless a bounded CLI query cannot answer the question.

## System vs feature classification

A system is a stable responsibility area with multiple collaborating classes or scene/prefab entry points. A feature is user-visible behavior or workflow supported by one or more systems. A class document is for a central implementation unit that reviewers need to understand independently.

## Creating documents

Create documents in the relevant folder with an `AUTO` summary block, an `AGENT:EXPLANATION` block, and a `Developer Notes` section. New system documents must cover purpose, responsibilities, entry points, core classes, execution flow, state/data flow, external dependencies, Scene/Prefab relationships, related features, source evidence, related wiki links, and uncertain inferences.

## Merging existing AGENT blocks

Preserve valid source-grounded prose. Replace claims only when source evidence changed or validation reports staleness. Keep manual content outside AGENT blocks untouched. If an existing AGENT block contains unsupported claims, mark uncertainty or remove the claim rather than inventing evidence.

## Progress and stop conditions

Save progress after each coherent batch by running build/validate and leaving documents in a consistent state. Stop and ask for guidance when scope candidates are dominated by third-party code, required source evidence is unavailable, or the document-plan cannot support a meaningful system/feature distinction.
