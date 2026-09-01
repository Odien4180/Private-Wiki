using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Model;
using Xunit;

namespace ProjectWiki.Core.Tests.Engine;

public class RelationImpactAnalyzerTests
{
    [Fact]
    public void Analyze_TraversesTheRelationGraphFromEntitiesBackedByChangedPaths()
    {
        var entities = new[]
        {
            Entity("a", "a.cs"),
            Entity("b", "b.cs"),
            Entity("c", "c.cs"),
            Entity("isolated", "isolated.cs"),
        };
        var relations = new[]
        {
            Relation("a", "b"),
            Relation("b", "c"),
        };

        var impact = RelationImpactAnalyzer.Analyze(
            new[] { new FileChangeRecord { Type = FileChangeType.Deleted, Path = "a.cs", PreviousHash = "a" } },
            entities,
            relations,
            entities.Where(entity => entity.Id != "a"),
            relations.Where(relation => relation.Source != "a"));

        Assert.Equal(new[] { "a" }, impact.DirectEntityIds);
        Assert.Equal(new[] { "b", "c" }, impact.RelatedEntityIds);
        Assert.Equal(new[] { "a", "b", "c" }, impact.AffectedEntityIds);
        Assert.Equal(2, impact.AffectedRelationCount);
    }

    private static Entity Entity(string id, string source) => new()
    {
        Id = id,
        Type = EntityType.Class,
        Title = id,
        Sources = new List<string> { source },
    };

    private static Relation Relation(string source, string target) => new()
    {
        Source = source,
        Target = target,
        Type = RelationType.Inherits,
        Confidence = Confidence.High,
    };
}
