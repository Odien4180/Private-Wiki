using ProjectWiki.Core.Model;

namespace ProjectWiki.Core.Engine;

/// <summary>Calculates deterministic source and relation-graph impact for a change set.</summary>
public static class RelationImpactAnalyzer
{
    public static RelationImpact Analyze(
        IEnumerable<FileChangeRecord> changes,
        IEnumerable<Entity> previousEntities,
        IEnumerable<Relation> previousRelations,
        IEnumerable<Entity> currentEntities,
        IEnumerable<Relation> currentRelations)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var changedPaths = changes
            .SelectMany(change => new[] { change.Path, change.PreviousPath })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var entities = previousEntities.Concat(currentEntities)
            .GroupBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entity => entity.Sources.Count).First())
            .ToList();
        var direct = entities
            .Where(entity => entity.Sources.Any(changedPaths.Contains))
            .Select(entity => entity.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var relations = previousRelations.Concat(currentRelations)
            .GroupBy(RelationKey.From, RelationKey.Comparer)
            .Select(group => group.First())
            .ToList();
        var neighbours = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var relation in relations)
        {
            AddNeighbour(neighbours, relation.Source, relation.Target);
            AddNeighbour(neighbours, relation.Target, relation.Source);
        }

        var affected = new HashSet<string>(direct, StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(direct);
        while (pending.TryDequeue(out var entityId))
        {
            if (!neighbours.TryGetValue(entityId, out var related))
            {
                continue;
            }

            foreach (var target in related)
            {
                if (affected.Add(target))
                {
                    pending.Enqueue(target);
                }
            }
        }

        var relatedEntityIds = affected.Except(direct, StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return new RelationImpact
        {
            DirectEntityIds = direct,
            RelatedEntityIds = relatedEntityIds,
            AffectedEntityIds = affected.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            AffectedRelationCount = relations.Count(relation =>
                affected.Contains(relation.Source) || affected.Contains(relation.Target)),
        };
    }

    private static void AddNeighbour(
        IDictionary<string, SortedSet<string>> neighbours,
        string source,
        string target)
    {
        if (!neighbours.TryGetValue(source, out var targets))
        {
            targets = new SortedSet<string>(StringComparer.Ordinal);
            neighbours[source] = targets;
        }

        targets.Add(target);
    }

    private readonly record struct RelationKey(string Source, string Target, RelationType Type)
    {
        public static IEqualityComparer<RelationKey> Comparer { get; } = new RelationKeyComparer();

        public static RelationKey From(Relation relation) => new(relation.Source, relation.Target, relation.Type);

        private sealed class RelationKeyComparer : IEqualityComparer<RelationKey>
        {
            public bool Equals(RelationKey x, RelationKey y) =>
                string.Equals(x.Source, y.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Target, y.Target, StringComparison.OrdinalIgnoreCase)
                && x.Type == y.Type;

            public int GetHashCode(RelationKey obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Source),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Target),
                    obj.Type);
        }
    }
}
