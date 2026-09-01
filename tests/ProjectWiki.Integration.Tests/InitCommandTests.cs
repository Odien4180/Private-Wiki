using System.Text.Json;
using Xunit;

namespace ProjectWiki.Integration.Tests;

public class InitCommandTests : IDisposable
{
    private readonly string _wikiRoot;
    private readonly string _fixtureRoot;

    public InitCommandTests()
    {
        _wikiRoot = Directory.CreateTempSubdirectory("project-wiki-integration-wiki-").FullName;
        _fixtureRoot = FindFixtureRoot();
    }

    public void Dispose()
    {
        Directory.Delete(_wikiRoot, recursive: true);
    }

    [Fact]
    public void Init_RunsAgainstFixtureProjectAndProducesValidKnowledgeGraph()
    {
        var (exitCode, stdOut, stdErr) = CliRunner.Run("init", "--project", _fixtureRoot, "--wiki", _wikiRoot, "--title", "Sample Game Wiki");

        Assert.True(exitCode == 0, $"CLI failed. stdout: {stdOut}\nstderr: {stdErr}");

        using var summary = JsonDocument.Parse(stdOut);
        Assert.Equal(3, summary.RootElement.GetProperty("entityCount").GetInt32());
        Assert.True(summary.RootElement.GetProperty("relationCount").GetInt32() >= 1);

        var entitiesJson = File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "entities.json"));
        using var entitiesDoc = JsonDocument.Parse(entitiesJson);
        var entities = entitiesDoc.RootElement.GetProperty("entities").EnumerateArray().ToList();
        Assert.Equal(3, entities.Count);
        Assert.Contains(entities, e => e.GetProperty("title").GetString() == "CombatManager");
        Assert.Contains(entities, e => e.GetProperty("title").GetString() == "IDamageable");
        Assert.Contains(entities, e => e.GetProperty("title").GetString() == "HitBox");

        var relationsJson = File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "relations.json"));
        using var relationsDoc = JsonDocument.Parse(relationsJson);
        var relations = relationsDoc.RootElement.GetProperty("relations").EnumerateArray().ToList();
        Assert.Contains(relations, r => r.GetProperty("type").GetString() == "implements");

        foreach (var relation in relations)
        {
            foreach (var evidence in relation.GetProperty("evidence").EnumerateArray())
            {
                var file = evidence.GetProperty("file").GetString();
                Assert.False(string.IsNullOrEmpty(file));
                Assert.True(File.Exists(Path.Combine(_fixtureRoot, file!)));
            }
        }

        var configJson = File.ReadAllText(Path.Combine(_wikiRoot, "wiki.config.json"));
        using var configDoc = JsonDocument.Parse(configJson);
        Assert.Equal("Sample Game Wiki", configDoc.RootElement.GetProperty("wiki").GetProperty("title").GetString());
    }

    [Fact]
    public void Init_FailsWithNonZeroExitCodeWhenProjectMissing()
    {
        var (exitCode, _, stdErr) = CliRunner.Run("init", "--project", Path.Combine(_wikiRoot, "missing"), "--wiki", _wikiRoot);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("does not exist", stdErr, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "SampleCSharpProject");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate tests/fixtures/SampleCSharpProject.");
    }
}
