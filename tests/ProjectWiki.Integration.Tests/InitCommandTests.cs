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

        var architectureDocument = File.ReadAllText(Path.Combine(_wikiRoot, "documents", "architecture", "overview.md"));
        Assert.Contains("3 extracted entities", architectureDocument);
    }

    [Fact]
    public void Init_FailsWithNonZeroExitCodeWhenProjectMissing()
    {
        var (exitCode, _, stdErr) = CliRunner.Run("init", "--project", Path.Combine(_wikiRoot, "missing"), "--wiki", _wikiRoot);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("does not exist", stdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RunsAgainstInitializedWiki()
    {
        var (initCode, _, initError) = CliRunner.Run("init", "--project", _fixtureRoot, "--wiki", _wikiRoot);
        Assert.True(initCode == 0, $"init failed: {initError}");

        var (exitCode, stdOut, stdErr) = CliRunner.Run("validate", "--wiki", _wikiRoot);

        Assert.True(exitCode == 0, $"validate failed. stdout: {stdOut}\nstderr: {stdErr}");
        using var result = JsonDocument.Parse(stdOut);
        Assert.True(result.RootElement.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public void UpdateRebuildAndInspect_OperateOnAnInitializedWiki()
    {
        var init = CliRunner.Run("init", "--project", _fixtureRoot, "--wiki", _wikiRoot);
        Assert.Equal(0, init.ExitCode);

        var (updateCode, updateOutput, updateError) = CliRunner.Run("update", "--wiki", _wikiRoot);
        Assert.True(updateCode == 0, $"update failed. stdout: {updateOutput}\nstderr: {updateError}");
        using var update = JsonDocument.Parse(updateOutput);
        Assert.False(update.RootElement.GetProperty("isRebuild").GetBoolean());
        Assert.Equal(0, update.RootElement.GetProperty("changes").GetArrayLength());

        var (inspectCode, inspectOutput, inspectError) = CliRunner.Run("inspect", "CombatManager", "--wiki", _wikiRoot);
        Assert.True(inspectCode == 0, $"inspect failed. stdout: {inspectOutput}\nstderr: {inspectError}");
        using var inspection = JsonDocument.Parse(inspectOutput);
        Assert.True(inspection.RootElement.GetProperty("isFound").GetBoolean());
        Assert.Equal("combat-manager", inspection.RootElement.GetProperty("entityId").GetString());

        var (rebuildCode, rebuildOutput, rebuildError) = CliRunner.Run("rebuild", "--wiki", _wikiRoot);
        Assert.True(rebuildCode == 0, $"rebuild failed. stdout: {rebuildOutput}\nstderr: {rebuildError}");
        using var rebuild = JsonDocument.Parse(rebuildOutput);
        Assert.True(rebuild.RootElement.GetProperty("isRebuild").GetBoolean());
    }

    [Fact]
    public void Build_WritesStaticSiteAndServeRejectsAnInvalidPort()
    {
        var init = CliRunner.Run("init", "--project", _fixtureRoot, "--wiki", _wikiRoot);
        Assert.Equal(0, init.ExitCode);

        var (buildCode, buildOutput, buildError) = CliRunner.Run("build", "--wiki", _wikiRoot);

        Assert.True(buildCode == 0, $"build failed. stdout: {buildOutput}\nstderr: {buildError}");
        using var build = JsonDocument.Parse(buildOutput);
        Assert.Equal(1, build.RootElement.GetProperty("documentCount").GetInt32());
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "site", "index.html")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "site", "search-index.json")));
        var (serveCode, _, serveError) = CliRunner.Run("serve", "--wiki", _wikiRoot, "--port", "0");
        Assert.NotEqual(0, serveCode);
        Assert.Contains("port", serveError, StringComparison.OrdinalIgnoreCase);
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
