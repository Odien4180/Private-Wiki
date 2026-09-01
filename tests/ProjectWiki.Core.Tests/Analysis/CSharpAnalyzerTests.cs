using ProjectWiki.Core.Analysis;
using ProjectWiki.Core.Model;
using Xunit;

namespace ProjectWiki.Core.Tests.Analysis;

public class CSharpAnalyzerTests : IDisposable
{
    private readonly string _root;

    public CSharpAnalyzerTests()
    {
        _root = Directory.CreateTempSubdirectory("project-wiki-analyzer-tests-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Analyze_ExtractsClassesInterfacesAndInheritance()
    {
        var damageableFile = WriteFile("IDamageable.cs", """
            namespace Game.Combat
            {
                public interface IDamageable
                {
                    void ApplyDamage(int amount);
                }
            }
            """);

        var characterFile = WriteFile("Character.cs", """
            namespace Game.Character
            {
                public class CharacterBase
                {
                }
            }
            """);

        var controllerFile = WriteFile("CharacterController.cs", """
            using Game.Character;
            using Game.Combat;

            namespace Game.Character
            {
                public class CharacterController : CharacterBase, IDamageable
                {
                    public void ApplyDamage(int amount) {}
                }
            }
            """);

        var analyzer = new CSharpAnalyzer();
        var result = analyzer.Analyze(new[]
        {
            new CSharpSourceFile("IDamageable.cs", damageableFile),
            new CSharpSourceFile("Character.cs", characterFile),
            new CSharpSourceFile("CharacterController.cs", controllerFile),
        });

        Assert.Equal(3, result.Entities.Count);

        var controller = Assert.Single(result.Entities, e => e.Title == "CharacterController");
        Assert.Equal(EntityType.Class, controller.Type);
        Assert.Contains("CharacterController.cs", controller.Sources);

        var damageable = Assert.Single(result.Entities, e => e.Title == "IDamageable");
        Assert.Equal(EntityType.Interface, damageable.Type);

        var characterBase = Assert.Single(result.Entities, e => e.Title == "CharacterBase");

        Assert.Contains(result.Relations, r =>
            r.Source == controller.Id && r.Target == characterBase.Id && r.Type == RelationType.Inherits && r.Confidence == Confidence.High);

        Assert.Contains(result.Relations, r =>
            r.Source == controller.Id && r.Target == damageable.Id && r.Type == RelationType.Implements && r.Confidence == Confidence.High);

        var inheritsRelation = result.Relations.Single(r => r.Type == RelationType.Inherits);
        var evidence = Assert.Single(inheritsRelation.Evidence);
        Assert.Equal("CharacterController.cs", evidence.File);
        Assert.True(evidence.StartLine > 0);
    }

    [Fact]
    public void Analyze_DoesNotCreateRelationsToUnresolvedExternalTypes()
    {
        var file = WriteFile("Widget.cs", """
            using System;

            namespace Game.UI
            {
                public class Widget : IDisposable
                {
                    public void Dispose() {}
                }
            }
            """);

        var analyzer = new CSharpAnalyzer();
        var result = analyzer.Analyze(new[] { new CSharpSourceFile("Widget.cs", file) });

        Assert.Single(result.Entities);
        Assert.Empty(result.Relations);
    }

    [Fact]
    public void Analyze_MergesPartialClassDeclarationsAcrossFiles()
    {
        var fileA = WriteFile("PartialA.cs", """
            namespace Game
            {
                public partial class Inventory
                {
                }
            }
            """);

        var fileB = WriteFile("PartialB.cs", """
            namespace Game
            {
                public partial class Inventory
                {
                    public int Capacity;
                }
            }
            """);

        var analyzer = new CSharpAnalyzer();
        var result = analyzer.Analyze(new[]
        {
            new CSharpSourceFile("PartialA.cs", fileA),
            new CSharpSourceFile("PartialB.cs", fileB),
        });

        var entity = Assert.Single(result.Entities);
        Assert.Equal(2, entity.Sources.Count);
        Assert.Contains("PartialA.cs", entity.Sources);
        Assert.Contains("PartialB.cs", entity.Sources);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
