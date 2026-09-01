namespace ProjectWiki.Core.Model;

/// <summary>
/// A directed edge in the project knowledge graph, connecting two
/// <see cref="Entity"/> ids.
/// </summary>
public sealed class Relation
{
    public required string Source { get; init; }

    public required string Target { get; init; }

    public required RelationType Type { get; init; }

    public required Confidence Confidence { get; init; }

    public List<Evidence> Evidence { get; init; } = new();
}
