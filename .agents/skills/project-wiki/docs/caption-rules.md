# Caption Rules

Captions explain why a source reference supports a document claim.

## Format

Use source captions in prose near the claim they support:

```markdown
> Source: `Assets/Scripts/PlayerController.cs:42` — Initializes player input for the gameplay loop.
```

When an end line is known, use `start-end`:

```markdown
> Source: `Assets/Scenes/Main.unity:120-148` — Scene object references the player prefab.
```

## Rules

- Line numbers must come from deterministic evidence or direct file inspection.
- Do not estimate or invent line numbers.
- If no line is available, cite only the path.
- Prefer evidence returned by `inspect` or `context` over reading large JSON files directly.
- Captions must describe the supported behavior, not merely repeat the file name.
- Do not create captions for ambiguous aliases.
- Regenerate backlinks after link/caption updates with `project-wiki build` or navigation rebuild.
