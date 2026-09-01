namespace ProjectWiki.Core.Model;

/// <summary>
/// A node in the project knowledge graph. Entities are produced
/// deterministically by the analyzers for symbol-level facts (classes,
/// interfaces, etc.); higher level entities (systems, features,
/// architecture) are expected to be added later by an agent, using the
/// same shape.
/// </summary>
public sealed class Entity
{
    /// <summary>Stable, kebab-case identifier that should survive reasonable refactoring.</summary>
    public required string Id { get; init; }

    public required EntityType Type { get; init; }

    public required string Title { get; init; }

    public List<string> Aliases { get; init; } = new();

    /// <summary>Project-relative paths this entity is backed by.</summary>
    public List<string> Sources { get; init; } = new();

    /// <summary>Fully-qualified or otherwise unambiguous symbol names for this entity, if any.</summary>
    public List<string> Symbols { get; init; } = new();
}
