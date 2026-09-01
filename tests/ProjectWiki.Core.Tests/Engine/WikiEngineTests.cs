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
        Assert.Equal(ProjectWiki.Core.Config.ProjectType.Generic, result.ProjectType);
        Assert.False(result.IsGitRepository);

        Assert.True(File.Exists(Path.Combine(_wikiRoot, "wiki.config.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "knowledge", "entities.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "knowledge", "relations.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "files.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "hashes.json")));
        Assert.True(File.Exists(Path.Combine(_wikiRoot, "tracking", "git.json")));
        Assert.True(Directory.Exists(Path.Combine(_wikiRoot, "documents", "systems")));
        Assert.True(Directory.Exists(Path.Combine(_wikiRoot, "site")));
        var architectureDocument = Path.Combine(_wikiRoot, "documents", "architecture", "overview.md");
        Assert.True(File.Exists(architectureDocument));
        Assert.Contains("2 extracted entities", File.ReadAllText(architectureDocument));

        var entitiesJson = File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "entities.json"));
        using var doc = JsonDocument.Parse(entitiesJson);
        var entities = doc.RootElement.GetProperty("entities");
        Assert.Equal(2, entities.GetArrayLength());

        var configJson = File.ReadAllText(Path.Combine(_wikiRoot, "wiki.config.json"));
        using var configDoc = JsonDocument.Parse(configJson);
        Assert.Equal("Test Wiki", configDoc.RootElement.GetProperty("wiki").GetProperty("title").GetString());
    }

    [Fact]
    public void Init_AppliesUnityScopeProfileAndWritesScopeReportAndPlan()
    {
        WriteProjectFile("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0");
        WriteProjectFile("Assets/Scripts/PlayerController.cs", "public class PlayerController { }");
        WriteProjectFile("Assets/AmplifyShaderEditor/VendorTool.cs", "public class VendorTool { }");
        WriteProjectFile("Assets/Plugins/ProjectPlugin.cs", "public class ProjectPlugin { }");

        var result = new WikiEngine().Init(new WikiInitOptions
        {
            ProjectRoot = _projectRoot,
            WikiRoot = _wikiRoot,
        });

        Assert.Equal(ProjectWiki.Core.Config.ProjectType.Unity, result.ProjectType);
        Assert.Equal(2, result.EntityCount);
        Assert.True(result.ExcludedFileCount > 0);
        Assert.True(result.ScopeReviewCandidateCount > 0);

        using var entitiesDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "entities.json")));
        var entityTitles = entitiesDoc.RootElement.GetProperty("entities").EnumerateArray()
            .Select(entity => entity.GetProperty("title").GetString())
            .ToList();
        Assert.Contains("PlayerController", entityTitles);
        Assert.Contains("ProjectPlugin", entityTitles);
        Assert.DoesNotContain("VendorTool", entityTitles);

        using var scopeDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "reports", "analysis-scope.json")));
        Assert.Contains("Assets/AmplifyShaderEditor/**", scopeDoc.RootElement.GetProperty("unityExclude").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(scopeDoc.RootElement.GetProperty("candidateFiles").EnumerateArray(), file =>
            file.GetProperty("path").GetString() == "Assets/Plugins/ProjectPlugin.cs");

        using var planDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "knowledge", "document-plan.json")));
        Assert.True(planDoc.RootElement.GetProperty("architecture").GetArrayLength() > 0);
        Assert.True(planDoc.RootElement.GetProperty("classes").GetArrayLength() > 0);
    }

    [Fact]
    public void QueryApis_FilterEntitiesAndContextBySourceAndTopic()
    {
        WriteProjectFile("Assets/Scripts/PlayerController.cs", "public class PlayerController { }");
        WriteProjectFile("Assets/UI/HudView.cs", "public class HudView { private PlayerController Player { get; set; } }");
        var engine = new WikiEngine();
        engine.Init(new WikiInitOptions { ProjectRoot = _projectRoot, WikiRoot = _wikiRoot });

        var list = engine.List(new WikiListOptions { WikiRoot = _wikiRoot, Type = "class", Source = "Assets/Scripts/**" });
        var context = engine.Context(new WikiContextOptions { WikiRoot = _wikiRoot, Topic = "Player", Depth = 2 });

        var listed = Assert.Single(list.Entities);
        Assert.Equal("PlayerController", listed.Title);
        Assert.Contains(context.Entities, entity => entity.Title == "PlayerController");
        Assert.All(context.Entities, entity => Assert.Equal("first_party", entity.CodeOwnership));
    }

    [Fact]
    public void ValidateRequireDocuments_FailsWhenOnlyOverviewExists()
    {
        WriteProjectFile("Alpha.cs", "public class Alpha { }");
        WriteProjectFile("Beta.cs", "public class Beta : Alpha { }");
        WriteProjectFile("Gamma.cs", "public class Gamma : Alpha { }");
        var engine = new WikiEngine();
        engine.Init(new WikiInitOptions { ProjectRoot = _projectRoot, WikiRoot = _wikiRoot });

        var validation = engine.ValidateNavigation(new ProjectWiki.Core.Navigation.WikiNavigationOptions
        {
            WikiRoot = _wikiRoot,
            RequireDocuments = true,
            MinCoverage = 0.7,
        });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.QualityIssues, issue => issue.Code == "no_system_documents");
        Assert.Contains(validation.QualityIssues, issue => issue.Code == "no_feature_documents");
        Assert.Contains(validation.QualityIssues, issue => issue.Code == "first_party_coverage_too_low");
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

    [Fact]
    public void Update_DetectsSourceChangesPreservesManualContentAndRecordsImpact()
    {
        WriteProjectFile("Alpha.cs", "public class Alpha { }");
        WriteProjectFile("Beta.cs", "public class Beta : Alpha { }");
        var engine = new WikiEngine();
        engine.Init(new WikiInitOptions { ProjectRoot = _projectRoot, WikiRoot = _wikiRoot });
        var overview = Path.Combine(_wikiRoot, "documents", "architecture", "overview.md");
        File.AppendAllText(overview, "Manual content must survive.");
        WriteProjectFile("Alpha.cs", "public class Alpha { public int Value { get; set; } }");

        var result = engine.Update(new WikiUpdateOptions { WikiRoot = _wikiRoot });

        var change = Assert.Single(result.Changes);
        Assert.Equal(FileChangeType.Modified, change.Type);
        Assert.Equal("Alpha.cs", change.Path);
        Assert.Contains("alpha", result.Impact.DirectEntityIds);
        Assert.Contains("beta", result.Impact.RelatedEntityIds);
        Assert.Contains("Manual content must survive.", File.ReadAllText(overview));
        using var updates = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "tracking", "updates.json")));
        var update = Assert.Single(updates.RootElement.GetProperty("updates").EnumerateArray());
        Assert.Equal("modified", update.GetProperty("changes")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void Update_DetectsRenamesAndInspect_ResolvesGeneratedAliases()
    {
        WriteProjectFile("OldFolder/Character.cs", "public class Character { }");
        var engine = new WikiEngine();
        engine.Init(new WikiInitOptions { ProjectRoot = _projectRoot, WikiRoot = _wikiRoot });
        Directory.CreateDirectory(Path.Combine(_projectRoot, "NewFolder"));
        File.Move(
            Path.Combine(_projectRoot, "OldFolder", "Character.cs"),
            Path.Combine(_projectRoot, "NewFolder", "Character.cs"));

        var update = engine.Update(new WikiUpdateOptions { WikiRoot = _wikiRoot });
        var rename = Assert.Single(update.Changes);
        Assert.Equal(FileChangeType.Renamed, rename.Type);
        Assert.Equal("OldFolder/Character.cs", rename.PreviousPath);
        Assert.Equal("NewFolder/Character.cs", rename.Path);
        var inspection = engine.Inspect(new WikiInspectOptions { WikiRoot = _wikiRoot, Entity = "Character" });

        Assert.True(inspection.IsFound);
        Assert.Equal("character", inspection.EntityId);
        Assert.Equal(new[] { "NewFolder/Character.cs" }, inspection.Entity!.Sources);
    }

    [Fact]
    public void Rebuild_PreservesManualDocumentsAndMarksTheUpdateRecord()
    {
        WriteProjectFile("Character.cs", "public class Character { }");
        var engine = new WikiEngine();
        engine.Init(new WikiInitOptions { ProjectRoot = _projectRoot, WikiRoot = _wikiRoot });
        var overview = Path.Combine(_wikiRoot, "documents", "architecture", "overview.md");
        File.AppendAllText(overview, "Manual rebuild note.");

        var result = engine.Rebuild(new WikiRebuildOptions { WikiRoot = _wikiRoot });

        Assert.True(result.IsRebuild);
        Assert.Contains("Manual rebuild note.", File.ReadAllText(overview));
        var updates = JsonDocument.Parse(File.ReadAllText(Path.Combine(_wikiRoot, "tracking", "updates.json")));
        Assert.True(updates.RootElement.GetProperty("updates")[0].GetProperty("isRebuild").GetBoolean());
    }

    private void WriteProjectFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
