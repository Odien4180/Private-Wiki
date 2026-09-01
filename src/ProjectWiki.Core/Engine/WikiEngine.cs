using System.Text.Json;
using ProjectWiki.Core.Analysis;
using ProjectWiki.Core.Config;
using ProjectWiki.Core.Documents;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Persistence;
using ProjectWiki.Core.Scanning;

namespace ProjectWiki.Core.Engine;

public sealed class WikiInitOptions
{
    public required string ProjectRoot { get; init; }

    public required string WikiRoot { get; init; }

    public string? Title { get; init; }

    public string Language { get; init; } = "ko";

    public IReadOnlyList<string> AdditionalExclusions { get; init; } = Array.Empty<string>();
}

public sealed class WikiInitResult
{
    public required string ProjectRoot { get; init; }

    public required string WikiRoot { get; init; }

    public required ProjectType ProjectType { get; init; }

    public required int ScannedFileCount { get; init; }

    public required int EntityCount { get; init; }

    public required int RelationCount { get; init; }

    public required bool IsGitRepository { get; init; }
}

/// <summary>
/// Orchestrates the deterministic <c>init</c> workflow: validate inputs,
/// detect project type, scan files, run static analysis, build the
/// knowledge graph, and persist it under <c>wiki_root</c>.
///
/// This is Milestone 1 scope only: no document generation, cross-linking,
/// captions, Unity parsing, or incremental update.
/// </summary>
public sealed class WikiEngine
{
    public WikiInitResult Init(WikiInitOptions options)
    {
        var projectRoot = Path.GetFullPath(options.ProjectRoot);
        var wikiRoot = Path.GetFullPath(options.WikiRoot);

        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException($"Project root does not exist: {projectRoot}");
        }

        Directory.CreateDirectory(wikiRoot);

        var projectType = ProjectTypeDetector.Detect(projectRoot);

        var scanner = new ProjectScanner();
        var scannedFiles = scanner.Scan(projectRoot, new ProjectScannerOptions
        {
            AdditionalExclusions = options.AdditionalExclusions,
        });

        var csharpFiles = scannedFiles
            .Where(f => string.Equals(f.Extension, ".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => new CSharpSourceFile(f.Path, Path.Combine(projectRoot, f.Path)))
            .ToList();

        var analysis = new CSharpAnalyzer().Analyze(csharpFiles);

        var gitInfo = GitRepositoryDetector.TryDetect(projectRoot);

        WriteConfig(wikiRoot, options, projectRoot, projectType);
        WriteKnowledge(wikiRoot, analysis);
        WriteTracking(wikiRoot, scannedFiles, gitInfo);
        CreateWikiSkeleton(wikiRoot);
        WriteInitialDocuments(wikiRoot, options, projectType, analysis);

        return new WikiInitResult
        {
            ProjectRoot = projectRoot,
            WikiRoot = wikiRoot,
            ProjectType = projectType,
            ScannedFileCount = scannedFiles.Count,
            EntityCount = analysis.Entities.Count,
            RelationCount = analysis.Relations.Count,
            IsGitRepository = gitInfo is not null,
        };
    }

    private static void WriteConfig(string wikiRoot, WikiInitOptions options, string projectRoot, ProjectType projectType)
    {
        var config = new WikiConfig
        {
            Project = new ProjectConfig { Root = projectRoot, Type = projectType },
            Wiki = new WikiMeta
            {
                Title = options.Title ?? new DirectoryInfo(projectRoot).Name,
                Language = options.Language,
            },
            Exclude = options.AdditionalExclusions.ToList(),
        };

        WriteJson(Path.Combine(wikiRoot, "wiki.config.json"), config);
    }

    private static void WriteKnowledge(string wikiRoot, AnalysisResult analysis)
    {
        var knowledgeDir = Path.Combine(wikiRoot, "knowledge");
        Directory.CreateDirectory(knowledgeDir);

        WriteJson(Path.Combine(knowledgeDir, "entities.json"), new { entities = analysis.Entities });
        WriteJson(Path.Combine(knowledgeDir, "relations.json"), new { relations = analysis.Relations });
        WriteJson(Path.Combine(knowledgeDir, "aliases.json"), new Dictionary<string, List<string>>());
        WriteJson(Path.Combine(knowledgeDir, "redirects.json"), new Dictionary<string, string>());
        WriteJson(Path.Combine(knowledgeDir, "captions.json"), new { captions = Array.Empty<object>() });
    }

    private static void WriteTracking(string wikiRoot, IReadOnlyList<ScannedFile> scannedFiles, GitInfo? gitInfo)
    {
        var trackingDir = Path.Combine(wikiRoot, "tracking");
        Directory.CreateDirectory(trackingDir);

        var files = new FilesTracking
        {
            Files = scannedFiles.Select(f => new TrackedFileRecord
            {
                Path = f.Path,
                Extension = f.Extension,
                Size = f.Size,
                ModifiedUtc = f.ModifiedUtc,
                Category = f.Category.ToString().ToLowerInvariant(),
            }).ToList(),
        };
        WriteJson(Path.Combine(trackingDir, "files.json"), files);

        var hashes = new HashesTracking();
        foreach (var f in scannedFiles)
        {
            hashes.Hashes[f.Path] = f.Hash;
        }

        WriteJson(Path.Combine(trackingDir, "hashes.json"), hashes);

        var git = new GitTracking
        {
            IsGitRepository = gitInfo is not null,
            LastIndexedCommit = gitInfo?.HeadCommit,
            Statuses = gitInfo is null ? new Dictionary<string, string>() : new Dictionary<string, string>(gitInfo.FileStatuses),
        };
        WriteJson(Path.Combine(trackingDir, "git.json"), git);

        WriteJson(Path.Combine(trackingDir, "updates.json"), new { updates = Array.Empty<object>() });
    }

    private static void CreateWikiSkeleton(string wikiRoot)
    {
        var directories = new[]
        {
            "documents/architecture",
            "documents/systems",
            "documents/features",
            "documents/classes",
            "documents/scenes",
            "documents/data",
            "documents/packages",
            "reports",
            "site",
        };

        foreach (var relative in directories)
        {
            Directory.CreateDirectory(Path.Combine(wikiRoot, relative));
        }

        private static void WriteInitialDocuments(
            string wikiRoot,
            WikiInitOptions options,
            ProjectType projectType,
            AnalysisResult analysis)
        {
            var plans = new InitialDocumentPlanner().Plan(new DocumentPlanningContext
            {
                WikiTitle = options.Title ?? new DirectoryInfo(Path.GetFullPath(options.ProjectRoot)).Name,
                ProjectType = projectType,
                Entities = analysis.Entities,
                Relations = analysis.Relations,
            });

            var store = new MarkdownDocumentStore();
            var documentsRoot = Path.Combine(wikiRoot, "documents");
            foreach (var plan in plans)
            {
                store.Write(documentsRoot, plan);
            }
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions.Default));
    }
}
