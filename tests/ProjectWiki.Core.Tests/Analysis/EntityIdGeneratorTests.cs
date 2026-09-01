using ProjectWiki.Core.Analysis;
using Xunit;

namespace ProjectWiki.Core.Tests.Analysis;

public class EntityIdGeneratorTests
{
    [Theory]
    [InlineData("CharacterController", "character-controller")]
    [InlineData("HP", "hp")]
    [InlineData("IInventory", "i-inventory")]
    [InlineData("Player", "player")]
    public void FromSymbolName_ConvertsPascalCaseToKebabCase(string input, string expected)
    {
        Assert.Equal(expected, EntityIdGenerator.FromSymbolName(input));
    }

    [Fact]
    public void MakeUnique_ReturnsPlainIdWhenNoCollision()
    {
        var used = new HashSet<string>();
        var id = EntityIdGenerator.MakeUnique("Player", "MyGame", used);
        Assert.Equal("player", id);
    }

    [Fact]
    public void MakeUnique_FallsBackToNamespaceQualifiedIdOnCollision()
    {
        var used = new HashSet<string>();
        var first = EntityIdGenerator.MakeUnique("Player", "MyGame.Core", used);
        var second = EntityIdGenerator.MakeUnique("Player", "MyGame.UI", used);

        Assert.Equal("player", first);
        Assert.NotEqual(first, second);
        Assert.Equal("my-game-ui-player", second);
    }

    [Fact]
    public void MakeUnique_NeverReturnsDuplicateIds()
    {
        var used = new HashSet<string>();
        var ids = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(EntityIdGenerator.MakeUnique("Player", "MyGame.Same", used));
        }

        Assert.Equal(5, ids.Count);
    }
}
