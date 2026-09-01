using System.Text.Json;
using Xunit;

namespace ProjectWiki.Integration.Tests;

public sealed class UnityCommandTests : IDisposable
{
    private readonly string _wikiRoot = Directory.CreateTempSubdirectory("project-wiki-unity-wiki-").FullName;

    [Fact]
    public void InitUpdateAndRebuild_PersistUnityKnowledgeGraphFacts()
    {
        var fixtureRoot = FindFixtureRoot();

        var (initCode, initOutput, initError) = CliRunner.Run("init", "--project", fixtureRoot, "--wiki", _wikiRoot);
        Assert.True(initCode == 0, $"init failed. stdout: {initOutput}\nstderr: {initError}");
        using var init = JsonDocument.Parse(initOutput);
        Assert.Equal("unity", init.RootElement.GetProperty("projectType").GetString());

        AssertUnityGraph();

        var (updateCode, updateOutput, updateError) = CliRunner.Run("update", "--wiki", _wikiRoot);
        Assert.True(updateCode == 0, $"update failed. stdout: {updateOutput}\nstderr: {updateError}");
        using var update = JsonDocument.Parse(updateOutput);
        Assert.False(update.RootElement.GetProperty("isRebuild").GetBoolean());
        AssertUnityGraph();

        var (rebuildCode, rebuildOutput, rebuildError) = CliRunner.Run("rebuild", "--wiki", _wikiRoot);
        Assert.True(rebuildCode == 0, $"rebuild failed. stdout: {rebuildOutput}\nstderr: {rebuildError}");
        using var rebuild = JsonDocument.Parse(rebuildOutput);
        Assert.True(rebuild.RootElement.GetProperty("isRebuild").GetBoolean());
        AssertUnityGraph();
    }

    public void Dispose() => Directory.Delete(_wikiRoot, recursive: true);

    private void AssertUnityGraph()
    {
        using var entitiesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "entities.json")));
        var entities = entitiesDocument.RootElement.GetProperty("entities").EnumerateArray().ToList();
        var scene = Assert.Single(entities.Where(entity => entity.GetProperty("type").GetString() == "scene"));
        var prefab = Assert.Single(entities.Where(entity => entity.GetProperty("type").GetString() == "prefab"));
        Assert.Contains(entities, entity =>
            entity.GetProperty("type").GetString() == "package"
            && entity.GetProperty("title").GetString() == "com.unity.timeline"
            && entity.GetProperty("members").EnumerateArray().Select(value => value.GetString()).Contains("version: 1.7.6"));

        using var relationsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "relations.json")));
        var relation = Assert.Single(relationsDocument.RootElement.GetProperty("relations").EnumerateArray().Where(item =>
            item.GetProperty("source").GetString() == scene.GetProperty("id").GetString()
            && item.GetProperty("target").GetString() == prefab.GetProperty("id").GetString()
            && item.GetProperty("type").GetString() == "references"));
        var evidence = Assert.Single(relation.GetProperty("evidence").EnumerateArray());
        Assert.Equal("Assets/Scenes/Main.unity", evidence.GetProperty("file").GetString());
        Assert.Equal(7, evidence.GetProperty("startLine").GetInt32());
    }

    private static string FindFixtureRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var fixture = Path.Combine(directory.FullName, "tests", "fixtures", "SampleUnityProject");
            if (Directory.Exists(fixture))
            {
                return fixture;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate tests/fixtures/SampleUnityProject.");
    }
}
