using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Navigation;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Tests.Navigation;

public class NavigationServiceTests : IDisposable
{
    private readonly string _wikiRoot = Directory.CreateTempSubdirectory("project-wiki-navigation-").FullName;

    public void Dispose() => Directory.Delete(_wikiRoot, recursive: true);

    [Fact]
    public void BuildNavigation_ResolvesAliasesAndRedirectsAndWritesBacklinks()
    {
        WriteEntities(CreateEntity("combat-system", "Combat System"), CreateEntity("character-system", "Character System"));
        var store = new NavigationStore();
        store.Initialize(_wikiRoot, ReadEntities());
        store.WriteRedirects(_wikiRoot, new RedirectIndex
        {
            Redirects = new List<RedirectEntry>
            {
                new() { From = "battle-system", To = "Combat System" },
            },
        });
        WriteDocument("architecture/overview.md", """
            [[battle-system|Battle]]
            [[character-system]]
            """);

        var result = new WikiEngine().BuildNavigation(new WikiNavigationOptions { WikiRoot = _wikiRoot });

        Assert.True(result.Validation.IsValid);
        Assert.Equal(1, result.DocumentCount);
        Assert.Equal(2, result.WikiLinkCount);
        Assert.Equal(2, result.ResolvedWikiLinkCount);

        var backlinks = store.Load(_wikiRoot).Backlinks;
        Assert.Collection(
            backlinks.Backlinks,
            character => Assert.Equal("character-system", character.Target),
            combat => Assert.Equal("combat-system", combat.Target));
        Assert.True(new WikiEngine().ValidateNavigation(new WikiNavigationOptions { WikiRoot = _wikiRoot }).IsValid);
    }

    [Fact]
    public void ValidateNavigation_ReportsAliasRedirectLinkAndBacklinkFailures()
    {
        WriteEntities(CreateEntity("one", "One"), CreateEntity("two", "Two"));
        var store = new NavigationStore();
        store.Initialize(_wikiRoot, ReadEntities());
        store.WriteAliases(_wikiRoot, new AliasIndex
        {
            Aliases = new List<AliasEntry>
            {
                new() { Alias = "Shared", Targets = new List<string> { "one" } },
                new() { Alias = "shared", Targets = new List<string> { "two" } },
                new() { Alias = "Ghost", Targets = new List<string> { "missing" } },
            },
        });
        store.WriteRedirects(_wikiRoot, new RedirectIndex
        {
            Redirects = new List<RedirectEntry>
            {
                new() { From = "old", To = "missing" },
                new() { From = "first", To = "second" },
                new() { From = "second", To = "first" },
            },
        });
        store.WriteBacklinks(_wikiRoot, new BacklinkIndex());
        WriteDocument("architecture/overview.md", "[[one]] [[Shared]] [[old]] [[unknown]]");

        var validation = new WikiEngine().ValidateNavigation(new WikiNavigationOptions { WikiRoot = _wikiRoot });
        var codes = validation.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.False(validation.IsValid);
        Assert.Contains("duplicate_alias", codes);
        Assert.Contains("broken_alias", codes);
        Assert.Contains("broken_redirect", codes);
        Assert.Contains("redirect_cycle", codes);
        Assert.Contains("ambiguous_wiki_link", codes);
        Assert.Contains("broken_wiki_link", codes);
        Assert.Contains("missing_backlink", codes);
    }

    private void WriteEntities(params Entity[] entities)
    {
        AtomicFile.WriteJson(Path.Combine(_wikiRoot, "knowledge", "entities.json"), new EntityCatalog
        {
            Entities = entities.ToList(),
        });
    }

    private List<Entity> ReadEntities() =>
        AtomicFile.ReadJson<EntityCatalog>(Path.Combine(_wikiRoot, "knowledge", "entities.json")).Entities;

    private void WriteDocument(string path, string content)
    {
        var fullPath = Path.Combine(_wikiRoot, "documents", path);
        AtomicFile.WriteText(fullPath, content);
    }

    private static Entity CreateEntity(string id, string title) => new()
    {
        Id = id,
        Type = EntityType.Class,
        Title = title,
    };
}
