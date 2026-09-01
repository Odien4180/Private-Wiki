using ProjectWiki.Core.Analysis;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Scanning;

namespace ProjectWiki.Core.Tests.Analysis;

public sealed class UnityAnalyzerTests : IDisposable
{
    private readonly string _projectRoot = Directory.CreateTempSubdirectory("project-wiki-unity-analyzer-").FullName;

    [Fact]
    public void Analyze_ExtractsUniqueGuidReferencesAssemblyDefinitionsAndManifestPackages()
    {
        Write("Assets/Scenes/Main.unity", """
            --- !u!114 &1
            MonoBehaviour:
              m_Prefab: {fileID: 1, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}
            """);
        WriteMeta("Assets/Scenes/Main.unity", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Write("Assets/Prefabs/Player.prefab", "--- !u!1 &2\nGameObject:\n");
        WriteMeta("Assets/Prefabs/Player.prefab", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        Write("Assets/Assemblies/Game.Core.asmdef", """{"name":"Game.Core","references":[]}""");
        WriteMeta("Assets/Assemblies/Game.Core.asmdef", "cccccccccccccccccccccccccccccccc");
        Write("Assets/Assemblies/Game.Play.asmdef", """{"name":"Game.Play","references":["Game.Core","GUID:cccccccccccccccccccccccccccccccc"]}""");
        WriteMeta("Assets/Assemblies/Game.Play.asmdef", "dddddddddddddddddddddddddddddddd");
        Write("Packages/manifest.json", """{"dependencies":{"com.unity.timeline":"1.7.6"}}""");

        var analysis = new UnityAnalyzer().Analyze(_projectRoot, new ProjectScanner().Scan(_projectRoot));

        var scene = Assert.Single(analysis.Entities.Where(entity => entity.Type == EntityType.Scene));
        var prefab = Assert.Single(analysis.Entities.Where(entity => entity.Type == EntityType.Prefab));
        var core = Assert.Single(analysis.Entities.Where(entity => entity.Title == "Game.Core"));
        var play = Assert.Single(analysis.Entities.Where(entity => entity.Title == "Game.Play"));
        var package = Assert.Single(analysis.Entities.Where(entity => entity.Type == EntityType.Package));
        Assert.Contains("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", scene.Symbols);
        Assert.Contains("version: 1.7.6", package.Members);

        var sceneReference = Assert.Single(analysis.Relations.Where(relation =>
            relation.Source == scene.Id && relation.Target == prefab.Id && relation.Type == RelationType.References));
        Assert.Equal(Confidence.High, sceneReference.Confidence);
        var evidence = Assert.Single(sceneReference.Evidence);
        Assert.Equal("Assets/Scenes/Main.unity", evidence.File);
        Assert.Equal(3, evidence.StartLine);
        Assert.Equal(3, evidence.EndLine);

        var assemblyReference = Assert.Single(analysis.Relations.Where(relation =>
            relation.Source == play.Id && relation.Target == core.Id && relation.Type == RelationType.DependsOn));
        Assert.Equal(Confidence.High, assemblyReference.Confidence);
        Assert.Equal("Assets/Assemblies/Game.Play.asmdef", Assert.Single(assemblyReference.Evidence).File);
    }

    [Fact]
    public void Analyze_DoesNotResolveUnknownOrAmbiguousGuidReferences()
    {
        Write("Assets/Scenes/Main.unity", """
            MonoBehaviour:
              m_Known: {fileID: 1, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}
              m_Unknown: {fileID: 1, guid: cccccccccccccccccccccccccccccccc, type: 3}
            """);
        WriteMeta("Assets/Scenes/Main.unity", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Write("Assets/Prefabs/One.prefab", string.Empty);
        WriteMeta("Assets/Prefabs/One.prefab", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        Write("Assets/Prefabs/Two.prefab", string.Empty);
        WriteMeta("Assets/Prefabs/Two.prefab", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var analysis = new UnityAnalyzer().Analyze(_projectRoot, new ProjectScanner().Scan(_projectRoot));

        Assert.Empty(analysis.Relations.Where(relation => relation.Type == RelationType.References));
    }

    public void Dispose() => Directory.Delete(_projectRoot, recursive: true);

    private void WriteMeta(string assetPath, string guid) =>
        Write($"{assetPath}.meta", $"fileFormatVersion: 2{Environment.NewLine}guid: {guid}{Environment.NewLine}");

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
