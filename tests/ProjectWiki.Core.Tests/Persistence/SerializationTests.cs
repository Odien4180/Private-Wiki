using System.Text.Json;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Persistence;
using Xunit;

namespace ProjectWiki.Core.Tests.Persistence;

public class SerializationTests
{
    [Fact]
    public void Entity_RoundTripsThroughJson()
    {
        var entity = new Entity
        {
            Id = "character-system",
            Type = EntityType.System,
            Title = "Character System",
            Aliases = new List<string> { "Character", "캐릭터 시스템" },
            Sources = new List<string> { "Assets/Scripts/Character" },
            Symbols = new List<string> { "CharacterController" },
        };

        var json = JsonSerializer.Serialize(entity, JsonOptions.Default);
        Assert.Contains("\"type\": \"system\"", json);

        var roundTripped = JsonSerializer.Deserialize<Entity>(json, JsonOptions.Default);
        Assert.NotNull(roundTripped);
        Assert.Equal(entity.Id, roundTripped!.Id);
        Assert.Equal(entity.Type, roundTripped.Type);
        Assert.Equal(entity.Aliases, roundTripped.Aliases);
    }

    [Fact]
    public void Relation_SerializesTypeAndConfidenceAsSnakeCase()
    {
        var relation = new Relation
        {
            Source = "character-system",
            Target = "combat-system",
            Type = RelationType.BelongsTo,
            Confidence = Confidence.High,
            Evidence = new List<Evidence>
            {
                new() { File = "Assets/Scripts/Character/Character.cs", StartLine = 10, EndLine = 12 },
            },
        };

        var json = JsonSerializer.Serialize(relation, JsonOptions.Default);
        Assert.Contains("\"type\": \"belongs_to\"", json);
        Assert.Contains("\"confidence\": \"high\"", json);

        var roundTripped = JsonSerializer.Deserialize<Relation>(json, JsonOptions.Default);
        Assert.NotNull(roundTripped);
        Assert.Equal(RelationType.BelongsTo, roundTripped!.Type);
        Assert.Equal(Confidence.High, roundTripped.Confidence);
        Assert.Single(roundTripped.Evidence);
    }
}
