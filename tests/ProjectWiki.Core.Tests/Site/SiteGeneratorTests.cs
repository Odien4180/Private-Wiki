using System.Text.Json;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Navigation;
using ProjectWiki.Core.Persistence;
using ProjectWiki.Core.Site;

namespace ProjectWiki.Core.Tests.Site;

public sealed class SiteGeneratorTests : IDisposable
{
    private readonly string _wikiRoot = Directory.CreateTempSubdirectory("project-wiki-site-").FullName;

    public void Dispose() => Directory.Delete(_wikiRoot, recursive: true);

    [Fact]
    public void BuildSite_RendersSafeMarkdownNavigationCaptionsBacklinksAndSearch()
    {
        AtomicFile.WriteJson(Path.Combine(_wikiRoot, "wiki.config.json"), new ProjectWiki.Core.Config.WikiConfig
        {
            Project = new ProjectWiki.Core.Config.ProjectConfig { Root = _wikiRoot },
            Wiki = new ProjectWiki.Core.Config.WikiMeta { Title = "Wiki <title>", Language = "en" },
        });
        AtomicFile.WriteJson(Path.Combine(_wikiRoot, "knowledge", "entities.json"), new EntityCatalog
        {
            Entities =
            [
                new Entity
                {
                    Id = "alpha",
                    Type = EntityType.Class,
                    Title = "Alpha",
                    Sources = ["Source/Alpha.cs"],
                },
            ],
        });
        new NavigationStore().Initialize(_wikiRoot, ReadEntities());
        AtomicFile.WriteJson(Path.Combine(_wikiRoot, "knowledge", "captions.json"), new
        {
            captions = new[]
            {
                new
                {
                    id = "alpha",
                    text = "Defined in <source>.",
                    source = new { file = "Source/Alpha.cs", startLine = 4, endLine = 6 },
                },
            },
        });
        AtomicFile.WriteText(Path.Combine(_wikiRoot, "documents", "architecture", "overview.md"), """
            # Overview

            ## Details
            [[Alpha]] and <script>alert(1)</script>.
            """);

        var result = new ProjectWiki.Core.Engine.WikiEngine().BuildSite(new WikiBuildOptions { WikiRoot = _wikiRoot });

        Assert.Equal(1, result.DocumentCount);
        Assert.Equal(1, result.EntityPageCount);
        Assert.Equal(2, result.SearchEntryCount);
        var document = File.ReadAllText(Path.Combine(_wikiRoot, "site", "architecture", "overview.html"));
        Assert.Contains("<aside><h2>Sidebar</h2>", document);
        Assert.Contains("id=\"details\"", document);
        Assert.Contains("href=\"../entities/alpha.html\"", document);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", document);
        var entity = File.ReadAllText(Path.Combine(_wikiRoot, "site", "entities", "alpha.html"));
        Assert.Contains("Backlinks", entity);
        Assert.Contains("Overview", entity);
        Assert.Contains("Defined in &lt;source&gt;.", entity);
        Assert.Contains("Source/Alpha.cs:4–6", entity);
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "site", "health.html")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "reports", "site-health.json")));
        var searchIndex = File.ReadAllText(Path.Combine(_wikiRoot, "site", "search-index.json"));
        Assert.Contains("\\u003Cscript\\u003E", searchIndex);
        using var parsedIndex = JsonDocument.Parse(searchIndex);
        Assert.Equal(2, parsedIndex.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void Serve_RejectsPortsOutsideTheTcpRange()
    {
        var engine = new ProjectWiki.Core.Engine.WikiEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Serve(new WikiServeOptions
        {
            WikiRoot = _wikiRoot,
            Port = 0,
        }));
    }

    private List<Entity> ReadEntities() =>
        AtomicFile.ReadJson<EntityCatalog>(Path.Combine(_wikiRoot, "knowledge", "entities.json")).Entities;
}
