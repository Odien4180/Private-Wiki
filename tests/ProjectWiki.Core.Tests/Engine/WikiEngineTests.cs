using System.Text.Json;
using ProjectWiki.Core.Engine;
using Xunit;

namespace ProjectWiki.Core.Tests.Engine;

public class WikiEngineTests : IDisposable
{
    private readonly string _projectRoot;
    private readonly string _wikiRoot;

    public WikiEngineTests()
    {
        _projectRoot = Directory.CreateTempSubdirectory("project-wiki-engine-project-").FullName;
        _wikiRoot = Directory.CreateTempSubdirectory("project-wiki-engine-wiki-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_projectRoot, recursive: true);
        Directory.Delete(_wikiRoot, recursive: true);
    }

    [Fact]
    public void Init_ProducesExpectedWikiStructureAndKnowledgeGraph()
    {
        WriteProjectFile("Assets/Scripts/CharacterBase.cs", """
            namespace Game.Character
            {
                public class CharacterBase
                {
                }
            }
            """);

        WriteProjectFile("Assets/Scripts/CharacterController.cs", """
            namespace Game.Character
            {
                public class CharacterController : CharacterBase
                {
                }
            }
            """);

        var engine = new WikiEngine();
        var result = engine.Init(new WikiInitOptions
        {
            ProjectRoot = _projectRoot,
            WikiRoot = _wikiRoot,
            Title = "Test Wiki",
        });

        Assert.Equal(2, result.EntityCount);
        Assert.Equal(1, result.RelationCount);
        Assert.False(result.IsGitRepository);

        Assert.True(File.Exists(Path.Combine(_wikiRoot, "wiki.config.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "knowledge", "entities.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "knowledge", "relations.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "files.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "hashes.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "git.json")));
        Assert.True(Directory.Exists(Path.Combine(_wikiRoot, "documents", "systems")));
        Assert.True(Directory.Exists(Path.Combine(_wikiRoot, "site")));

        var entitiesJson = File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "entities.json"));
        using var doc = JsonDocument.Parse(entitiesJson);
        var entities = doc.RootElement.GetProperty("entities");
        Assert.Equal(2, entities.GetArrayLength());

        var configJson = File.ReadAllText(Path.Combine(_wikiRoot, "wiki.config.json"));
        using var configDoc = JsonDocument.Parse(configJson);
        Assert.Equal("Test Wiki", configDoc.RootElement.GetProperty("wiki").GetProperty("title").GetString());
    }

    [Fact]
    public void Init_ThrowsWhenProjectRootDoesNotExist()
    {
        var engine = new WikiEngine();
        Assert.Throws<DirectoryNotFoundException>(() => engine.Init(new WikiInitOptions
        {
            ProjectRoot = Path.Combine(_projectRoot, "does-not-exist"),
            WikiRoot = _wikiRoot,
        }));
    }

    private void WriteProjectFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
