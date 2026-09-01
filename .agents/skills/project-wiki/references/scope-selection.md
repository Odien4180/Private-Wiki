# Scope Selection

Use this reference before first initialization and whenever validation reports third-party noise.

## Preview

Run:

```bash
project-wiki scope --project <project> --summary
project-wiki scope --project <project> --limit 100
```

Review included file count, excluded file count, Unity automatic exclusions, and candidate files. Detailed init/update reports are written to `reports/analysis-scope.json`.

## Unity defaults

Automatically excluded third-party Unity folders include Amplify Shader, Amplify Shader Pack, NiloToonURP, `Assets/Packages`, and TextMesh Pro. `Assets/Plugins/**` is a review candidate only because first-party code is often mixed there.

## Include/exclude refinement

Use `init --include <glob>` to narrow analysis to first-party source roots and `init --exclude <glob>` for confirmed external packages. Do not exclude a folder only because its name looks generic; use the preview and source evidence.

## Large project safety

Never paste unbounded scope JSON into agent context. Use `--summary` first, then bounded `--limit` previews. If the summary still shows high third-party noise, refine scope before authoring.
