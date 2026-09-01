using ProjectWiki.Core.Analysis;
using ProjectWiki.Core.Config;
using ProjectWiki.Core.Documents;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Navigation;
using ProjectWiki.Core.Persistence;
using ProjectWiki.Core.Scanning;
using ProjectWiki.Core.Site;

namespace ProjectWiki.Core.Engine;

public sealed class WikiInitOptions
{
    public required string ProjectRoot { get; init; }

    public required string WikiRoot { get; init; }

    public string? Title { get; init; }

    public string Language { get; init; } = "ko";

    public IReadOnlyList<string> AdditionalExclusions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> IncludePatterns { get; init; } = Array.Empty<string>();
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

    public required int ExcludedFileCount { get; init; }

    public required int ScopeReviewCandidateCount { get; init; }
}

/// <summary>
/// Orchestrates the deterministic <c>init</c> workflow: validate inputs,
/// detect project type, scan files, run static analysis, build the
/// knowledge graph, and persist it under <c>wiki_root</c>.
///
/// This includes initial documents and the deterministic navigation index.
/// Incremental source updates preserve human-authored document content by
/// replacing only known AUTO blocks.
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

        if (IsWithin(projectRoot, wikiRoot))
        {
            throw new ArgumentException("Wiki root must not be inside the project root.");
        }

        Directory.CreateDirectory(wikiRoot);

        var projectType = ProjectTypeDetector.Detect(projectRoot);

        var scopeAnalyzer = new ProjectScopeAnalyzer();
        var scope = scopeAnalyzer.Analyze(projectRoot, projectType, options.AdditionalExclusions, options.IncludePatterns);
        var scanner = new ProjectScanner();
        var scannedFiles = scanner.Scan(projectRoot, new ProjectScannerOptions
        {
            AdditionalExclusions = options.AdditionalExclusions
                .Concat(projectType == ProjectType.Unity ? UnityExclusionProfile.AutomaticExclusions : Array.Empty<string>())
                .ToList(),
            IncludePatterns = options.IncludePatterns,
        });

        var analysis = AnalyzeProject(projectRoot, scannedFiles, projectType);

        var gitInfo = GitRepositoryDetector.TryDetect(projectRoot);

        WriteConfig(wikiRoot, options, projectRoot, projectType);
        scopeAnalyzer.WriteReport(wikiRoot, scope);
        WriteKnowledge(wikiRoot, analysis, initializeNavigation: true);
        WriteTracking(wikiRoot, scannedFiles, gitInfo, resetUpdates: true);
        CreateWikiSkeleton(wikiRoot);
        WriteInitialDocuments(wikiRoot, options, projectType, analysis);
        BuildNavigation(new WikiNavigationOptions { WikiRoot = wikiRoot });

        return new WikiInitResult
        {
            ProjectRoot = projectRoot,
            WikiRoot = wikiRoot,
            ProjectType = projectType,
            ScannedFileCount = scannedFiles.Count,
            EntityCount = analysis.Entities.Count,
            RelationCount = analysis.Relations.Count,
            IsGitRepository = gitInfo is not null,
            ExcludedFileCount = scope.ExcludedFileCount,
            ScopeReviewCandidateCount = scope.CandidateFiles.Count,
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
            Include = options.IncludePatterns.ToList(),
        };

        AtomicFile.WriteJson(Path.Combine(wikiRoot, "wiki.config.json"), config);
    }

    private static void WriteKnowledge(string wikiRoot, AnalysisResult analysis, bool initializeNavigation)
    {
        var knowledgeDir = Path.Combine(wikiRoot, "knowledge");
        Directory.CreateDirectory(knowledgeDir);

        AtomicFile.WriteJson(Path.Combine(knowledgeDir, "entities.json"), new EntityCatalog
        {
            Entities = analysis.Entities.OrderBy(e => e.Id, StringComparer.Ordinal).ToList(),
        });
        AtomicFile.WriteJson(Path.Combine(knowledgeDir, "relations.json"), new RelationCatalog
        {
            Relations = OrderRelations(analysis.Relations),
        });
        AtomicFile.WriteJson(Path.Combine(knowledgeDir, "document-plan.json"), CreateKnowledgeDocumentPlan(analysis));

        var navigationStore = new NavigationStore();
        if (initializeNavigation)
        {
            navigationStore.Initialize(wikiRoot, analysis.Entities);
            AtomicFile.WriteJson(Path.Combine(knowledgeDir, "captions.json"), new { captions = Array.Empty<object>() });
        }
        else
        {
            navigationStore.RefreshAliases(wikiRoot, analysis.Entities);
        }
    }

    private static void WriteTracking(
        string wikiRoot,
        IReadOnlyList<ScannedFile> scannedFiles,
        GitInfo? gitInfo,
        UpdateRecord? update = null,
        bool resetUpdates = false)
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
        AtomicFile.WriteJson(Path.Combine(trackingDir, "files.json"), files);

        var hashes = new HashesTracking();
        foreach (var f in scannedFiles)
        {
            hashes.Hashes[f.Path] = f.Hash;
        }

        hashes.Hashes = hashes.Hashes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        AtomicFile.WriteJson(Path.Combine(trackingDir, "hashes.json"), hashes);

        var git = new GitTracking
        {
            IsGitRepository = gitInfo is not null,
            LastIndexedCommit = gitInfo?.HeadCommit,
            Statuses = gitInfo is null ? new Dictionary<string, string>() : new Dictionary<string, string>(gitInfo.FileStatuses),
        };
        AtomicFile.WriteJson(Path.Combine(trackingDir, "git.json"), git);

        var updatesPath = Path.Combine(trackingDir, "updates.json");
        var priorUpdates = File.Exists(updatesPath)
            ? AtomicFile.ReadJson<UpdatesTracking>(updatesPath)
            : new UpdatesTracking();
        AtomicFile.WriteJson(updatesPath, new UpdatesTracking
        {
            Updates = update is null
                ? (resetUpdates ? new List<UpdateRecord>() : priorUpdates.Updates)
                : priorUpdates.Updates.Append(update).ToList(),
        });
    }

    private static void CreateWikiSkeleton(string wikiRoot)
    {
        var directories = new[]
        {
            "documents/project",
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
    }

    private static void WriteInitialDocuments(
        string wikiRoot,
        WikiInitOptions options,
        ProjectType projectType,
        AnalysisResult analysis)
        => WriteInitialDocuments(
            wikiRoot,
            options.Title ?? new DirectoryInfo(Path.GetFullPath(options.ProjectRoot)).Name,
            projectType,
            analysis);

    private static void WriteInitialDocuments(
        string wikiRoot,
        string title,
        ProjectType projectType,
        AnalysisResult analysis)
    {
        var plans = new InitialDocumentPlanner().Plan(new DocumentPlanningContext
        {
            WikiTitle = title,
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

    /// <summary>
    /// Rebuilds the deterministic backlink index from the Markdown documents
    /// already stored in a wiki. Alias and redirect definitions are preserved.
    /// </summary>
    public WikiNavigationResult BuildNavigation(WikiNavigationOptions options)
    {
        var wikiRoot = ValidateWikiRoot(options);
        return new NavigationService().Build(wikiRoot);
    }

    /// <summary>
    /// Validates persisted aliases, redirects, Markdown wiki links, and the
    /// backlink index without changing the wiki.
    /// </summary>
    public NavigationValidationResult ValidateNavigation(WikiNavigationOptions options)
    {
        var wikiRoot = ValidateWikiRoot(options);
        return new NavigationService().Validate(wikiRoot, options.RequireDocuments, options.MinCoverage);
    }

    /// <summary>
    /// Builds a deterministic static HTML site from the persisted Markdown
    /// documents and navigation data.
    /// </summary>
    public WikiBuildResult BuildSite(WikiBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        return new SiteGenerator().Build(wikiRoot);
    }

    /// <summary>
    /// Builds the static site and serves it over an IPv4 loopback-only HTTP
    /// listener until the supplied cancellation token is cancelled.
    /// </summary>
    public WikiServeResult Serve(WikiServeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        return new LocalSiteServer().Serve(wikiRoot, options.Port, cancellationToken);
    }

    /// <summary>
    /// Reindexes a persisted wiki's configured project. Changes are derived
    /// solely from SHA-256 snapshots; git metadata remains informational.
    /// </summary>
    public WikiUpdateResult Update(WikiUpdateOptions options) => Reindex(options, isRebuild: false);

    /// <summary>
    /// Reindexes the entire configured project while preserving custom
    /// documents, aliases, redirects, and non-analyzer graph records.
    /// </summary>
    public WikiUpdateResult Rebuild(WikiRebuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Reindex(new WikiUpdateOptions { WikiRoot = options.WikiRoot }, isRebuild: true);
    }

    /// <summary>Finds one entity through its id, alias, or redirect and returns its local graph context.</summary>
    public WikiInspectResult Inspect(WikiInspectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        var data = new NavigationStore().Load(wikiRoot);
        var resolution = new NavigationResolver(data).Resolve(options.Entity);
        if (resolution.Status != NavigationResolutionStatus.Resolved)
        {
            return new WikiInspectResult
            {
                Query = options.Entity,
                IsFound = false,
                IsAmbiguous = resolution.Status == NavigationResolutionStatus.Ambiguous,
            };
        }

        var entityId = resolution.EntityId!;
        var depth = Math.Max(1, options.Depth);
        var relationCatalog = LoadRelations(wikiRoot);
        var relatedIds = ExpandRelatedIds(entityId, relationCatalog, depth);
        var relations = relationCatalog
            .Where(relation => string.Equals(relation.Source, entityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(relation.Target, entityId, StringComparison.OrdinalIgnoreCase)
                || (depth > 1 && relatedIds.Contains(relation.Source) && relatedIds.Contains(relation.Target)));
        var backlinks = data.Backlinks.Backlinks
            .Where(backlink => string.Equals(backlink.Target, entityId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(backlink => backlink.References)
            .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
            .ThenBy(reference => reference.Line)
            .ThenBy(reference => reference.Column)
            .ThenBy(reference => reference.LinkTarget, StringComparer.Ordinal)
            .ToList();
        return new WikiInspectResult
        {
            Query = options.Entity,
            IsFound = true,
            IsAmbiguous = false,
            EntityId = entityId,
            Entity = data.Entities.Entities.Single(entity =>
                string.Equals(entity.Id, entityId, StringComparison.OrdinalIgnoreCase)),
            Relations = OrderRelations(relations),
            Backlinks = backlinks,
        };
    }

    public WikiListResult List(WikiListOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        var data = new NavigationStore().Load(wikiRoot);
        var entities = data.Entities.Entities.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(options.Type))
        {
            if (!Enum.TryParse<EntityType>(options.Type.Replace("-", string.Empty).Replace("_", string.Empty), ignoreCase: true, out var entityType))
            {
                throw new ArgumentException($"Unknown entity type: {options.Type}");
            }

            entities = entities.Where(entity => entity.Type == entityType);
        }

        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            entities = entities.Where(entity => entity.Sources.Any(source => GlobMatcher.IsMatch(source, options.Source)));
        }

        var summaries = entities
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .Select(CreateEntitySummary)
            .ToList();
        var offset = Math.Max(0, options.Offset);
        var limit = NormalizeLimit(options.Limit, defaultLimit: 100);
        var page = summaries.Skip(offset).Take(limit).ToList();
        return new WikiListResult
        {
            TotalCount = summaries.Count,
            Offset = offset,
            Limit = limit,
            Count = page.Count,
            Entities = page,
        };
    }

    public WikiContextResult Context(WikiContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        var data = new NavigationStore().Load(wikiRoot);
        var relations = LoadRelations(wikiRoot);
        var seeds = data.Entities.Entities.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            seeds = seeds.Where(entity => entity.Sources.Any(source => GlobMatcher.IsMatch(source, options.Source)));
        }

        if (!string.IsNullOrWhiteSpace(options.Topic))
        {
            var topic = options.Topic.Trim();
            seeds = seeds.Where(entity =>
                Contains(entity.Id, topic)
                || Contains(entity.Title, topic)
                || Contains(entity.Namespace, topic)
                || entity.Members.Any(member => Contains(member, topic))
                || entity.Sources.Any(source => Contains(source, topic)));
        }

        var limit = NormalizeLimit(options.Limit, defaultLimit: 50);
        var seedIds = seeds
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .Take(limit)
            .Select(entity => entity.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var depth = Math.Max(1, options.Depth);
        foreach (var seed in seedIds.ToList())
        {
            seedIds.UnionWith(ExpandRelatedIds(seed, relations, depth));
        }

        var incoming = OrderRelations(relations.Where(relation => seedIds.Contains(relation.Target) && !seedIds.Contains(relation.Source)));
        var outgoing = OrderRelations(relations.Where(relation => seedIds.Contains(relation.Source)));
        var backlinks = data.Backlinks.Backlinks
            .Where(backlink => seedIds.Contains(backlink.Target))
            .SelectMany(backlink => backlink.References)
            .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
            .ThenBy(reference => reference.Line)
            .ThenBy(reference => reference.Column)
            .ToList();

        return new WikiContextResult
        {
            Query = options.Topic ?? options.Source ?? string.Empty,
            Entities = data.Entities.Entities
                .Where(entity => seedIds.Contains(entity.Id))
                .OrderBy(entity => entity.Id, StringComparer.Ordinal)
                .Take(limit)
                .Select(CreateEntitySummary)
                .ToList(),
            IncomingRelations = incoming.Take(limit).ToList(),
            OutgoingRelations = outgoing.Take(limit).ToList(),
            Backlinks = backlinks.Take(limit).ToList(),
        };
    }

    private WikiUpdateResult Reindex(WikiUpdateOptions options, bool isRebuild)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = ValidateWikiRoot(new WikiNavigationOptions { WikiRoot = options.WikiRoot });
        var config = AtomicFile.ReadJson<WikiConfig>(Path.Combine(wikiRoot, "wiki.config.json"));
        var projectRoot = Path.GetFullPath(config.Project.Root);
        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException($"Project root does not exist: {projectRoot}");
        }

        var currentFiles = new ProjectScanner().Scan(projectRoot, new ProjectScannerOptions
        {
            AdditionalExclusions = config.Exclude
                .Concat(config.Project.Type == ProjectType.Unity ? UnityExclusionProfile.AutomaticExclusions : Array.Empty<string>())
                .ToList(),
            IncludePatterns = config.Include,
            UseGit = config.Analysis.Git,
        });
        var scopeAnalyzer = new ProjectScopeAnalyzer();
        scopeAnalyzer.WriteReport(
            wikiRoot,
            scopeAnalyzer.Analyze(projectRoot, config.Project.Type, config.Exclude, config.Include));
        var previousHashes = LoadHashes(wikiRoot);
        var changes = ChangeDetector.Detect(previousHashes, currentFiles);
        var previousEntities = new NavigationStore().Load(wikiRoot).Entities.Entities;
        var previousRelations = LoadRelations(wikiRoot);
        var currentAnalysis = AnalyzeProject(projectRoot, currentFiles, config.Project.Type);
        var analysis = MergeAnalysis(previousEntities, previousRelations, currentAnalysis);
        var impact = RelationImpactAnalyzer.Analyze(
            changes,
            previousEntities,
            previousRelations,
            analysis.Entities,
            analysis.Relations);
        var gitInfo = config.Analysis.Git ? GitRepositoryDetector.TryDetect(projectRoot) : null;

        CreateWikiSkeleton(wikiRoot);
        WriteKnowledge(wikiRoot, analysis, initializeNavigation: false);
        WriteTracking(wikiRoot, currentFiles, gitInfo, new UpdateRecord
        {
            IndexedUtc = DateTime.UtcNow,
            IsRebuild = isRebuild,
            Changes = changes.ToList(),
            Impact = impact,
        });
        WriteInitialDocuments(wikiRoot, config.Wiki.Title, config.Project.Type, analysis);
        BuildNavigation(new WikiNavigationOptions { WikiRoot = wikiRoot });

        return new WikiUpdateResult
        {
            WikiRoot = wikiRoot,
            IsRebuild = isRebuild,
            ScannedFileCount = currentFiles.Count,
            EntityCount = analysis.Entities.Count,
            RelationCount = analysis.Relations.Count,
            IsGitRepository = gitInfo is not null,
            Changes = changes.ToList(),
            Impact = impact,
        };
    }

    private static AnalysisResult AnalyzeProject(
        string projectRoot,
        IReadOnlyList<ScannedFile> scannedFiles,
        ProjectType projectType)
    {
        var csharpAnalysis = new CSharpAnalyzer().Analyze(scannedFiles
            .Where(file => string.Equals(file.Extension, ".cs", StringComparison.OrdinalIgnoreCase))
            .Select(file => new CSharpSourceFile(file.Path, Path.Combine(projectRoot, file.Path)))
            .ToList());
        if (projectType != ProjectType.Unity)
        {
            return csharpAnalysis;
        }

        var unityAnalysis = new UnityAnalyzer().Analyze(
            projectRoot,
            scannedFiles,
            csharpAnalysis.Entities.Select(entity => entity.Id));
        return new AnalysisResult
        {
            Entities = csharpAnalysis.Entities.Concat(unityAnalysis.Entities)
                .OrderBy(entity => entity.Id, StringComparer.Ordinal)
                .ToList(),
            Relations = OrderRelations(csharpAnalysis.Relations.Concat(unityAnalysis.Relations)),
        };
    }

    private static Dictionary<string, string> LoadHashes(string wikiRoot)
    {
        var path = Path.Combine(wikiRoot, "tracking", "hashes.json");
        return File.Exists(path)
            ? AtomicFile.ReadJson<HashesTracking>(path).Hashes
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static List<Relation> LoadRelations(string wikiRoot)
    {
        var path = Path.Combine(wikiRoot, "knowledge", "relations.json");
        return File.Exists(path)
            ? AtomicFile.ReadJson<RelationCatalog>(path).Relations
            : new List<Relation>();
    }

    private static AnalysisResult MergeAnalysis(
        IEnumerable<Entity> previousEntities,
        IEnumerable<Relation> previousRelations,
        AnalysisResult currentAnalysis)
    {
        var priorEntities = previousEntities
            .GroupBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(entity => entity.Title, StringComparer.Ordinal).First(),
                StringComparer.OrdinalIgnoreCase);
        var entities = currentAnalysis.Entities
            .Select(entity => priorEntities.TryGetValue(entity.Id, out var prior)
                ? new Entity
                {
                    Id = entity.Id,
                    Type = entity.Type,
                    Title = entity.Title,
                    Aliases = prior.Aliases.Concat(entity.Aliases).Distinct(StringComparer.Ordinal).OrderBy(alias => alias, StringComparer.Ordinal).ToList(),
                    Sources = entity.Sources,
                    Symbols = entity.Symbols,
                    Namespace = entity.Namespace,
                    Members = entity.Members,
                    Attributes = entity.Attributes,
                }
                : entity)
            .ToList();
        var currentIds = entities.Select(entity => entity.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        entities.AddRange(previousEntities
            .Where(entity => !IsAnalyzerManaged(entity) && currentIds.Add(entity.Id)));

        var relations = currentAnalysis.Relations.ToList();
        var relationKeys = relations
            .Select(RelationKey.From)
            .ToHashSet(RelationKey.Comparer);
        relations.AddRange(previousRelations.Where(relation =>
            !IsAnalyzerManaged(relation) && relationKeys.Add(RelationKey.From(relation))));
        return new AnalysisResult
        {
            Entities = entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToList(),
            Relations = OrderRelations(relations),
        };
    }

    private static bool IsAnalyzerManaged(Entity entity) =>
        UnityAnalyzer.IsManaged(entity)
        || (entity.Sources.Count > 0
            && entity.Type is EntityType.Class or EntityType.Struct or EntityType.Interface or EntityType.Enum);

    private static bool IsAnalyzerManaged(Relation relation) =>
        UnityAnalyzer.IsManaged(relation)
        || (relation.Type is RelationType.Inherits or RelationType.Implements
            && relation.Confidence == Confidence.High
            && relation.Evidence.Count > 0);

    private static KnowledgeDocumentPlan CreateKnowledgeDocumentPlan(AnalysisResult analysis)
    {
        var relationsByEntity = analysis.Relations
            .SelectMany(relation => new[] { relation.Source, relation.Target })
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var firstPartyEntities = analysis.Entities
            .Where(entity => CodeOwnershipClassifier.Classify(entity) == "first_party")
            .ToList();

        var plan = new KnowledgeDocumentPlan
        {
            Architecture =
            [
                new DocumentPlanCandidate
                {
                    Id = "architecture-overview",
                    Title = "Architecture Overview",
                    Reason = "Project-wide deterministic summary.",
                    EntityIds = firstPartyEntities.Select(entity => entity.Id).Take(25).ToList(),
                    Sources = firstPartyEntities.SelectMany(entity => entity.Sources).Distinct(StringComparer.Ordinal).Take(25).ToList(),
                },
            ],
            Systems = CreateGroupCandidates(firstPartyEntities, entity => GetSystemGroup(entity), "system").Take(12).ToList(),
            Features = CreateGroupCandidates(firstPartyEntities, entity => GetFeatureGroup(entity), "feature").Take(12).ToList(),
            Classes = firstPartyEntities
                .Where(entity => entity.Type is EntityType.Class or EntityType.Interface or EntityType.Struct or EntityType.Enum)
                .OrderByDescending(entity => relationsByEntity.GetValueOrDefault(entity.Id))
                .ThenBy(entity => entity.Id, StringComparer.Ordinal)
                .Take(25)
                .Select(entity => new DocumentPlanCandidate
                {
                    Id = entity.Id,
                    Title = entity.Title,
                    Reason = $"Important source entity with {relationsByEntity.GetValueOrDefault(entity.Id)} graph references.",
                    EntityIds = [entity.Id],
                    Sources = entity.Sources,
                    Evidence = entity.Sources,
                })
                .ToList(),
            Scenes = CreateTypedCandidates(analysis.Entities, EntityType.Scene),
            Data = CreateTypedCandidates(analysis.Entities, EntityType.Data),
            Packages = CreateTypedCandidates(analysis.Entities, EntityType.Package),
        };

        return plan;
    }

    private static IEnumerable<DocumentPlanCandidate> CreateGroupCandidates(
        IReadOnlyList<Entity> entities,
        Func<Entity, string?> groupSelector,
        string kind)
        => entities
            .Select(entity => new { Entity = entity, Group = groupSelector(entity) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Group))
            .GroupBy(item => item.Group!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new DocumentPlanCandidate
            {
                Id = $"{kind}-{NormalizePlanId(group.Key)}",
                Title = group.Key,
                Reason = $"Deterministic {kind} candidate from folder/namespace/assembly clustering.",
                EntityIds = group.Select(item => item.Entity.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Sources = group.SelectMany(item => item.Entity.Sources).Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal).Take(50).ToList(),
                Evidence = group.SelectMany(item => item.Entity.Sources).Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal).Take(10).ToList(),
            });

    private static List<DocumentPlanCandidate> CreateTypedCandidates(IEnumerable<Entity> entities, EntityType type)
        => entities
            .Where(entity => entity.Type == type)
            .OrderBy(entity => entity.Id, StringComparer.Ordinal)
            .Take(50)
            .Select(entity => new DocumentPlanCandidate
            {
                Id = entity.Id,
                Title = entity.Title,
                Reason = $"{type} entity extracted from project analysis.",
                EntityIds = [entity.Id],
                Sources = entity.Sources,
                Evidence = entity.Sources,
            })
            .ToList();

    private static string? GetSystemGroup(Entity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Namespace))
        {
            var parts = entity.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return string.Join('.', parts.Take(2));
            }
        }

        var source = entity.Sources.FirstOrDefault();
        if (source is null)
        {
            return null;
        }

        var segments = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? string.Join('/', segments.Take(2)) : segments[0];
    }

    private static string? GetFeatureGroup(Entity entity)
    {
        var source = entity.Sources.FirstOrDefault();
        if (source is null)
        {
            return null;
        }

        var segments = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('/', segments.Take(3));
        }

        return segments.Length >= 2 ? string.Join('/', segments.Take(2)) : null;
    }

    private static string NormalizePlanId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static EntitySummary CreateEntitySummary(Entity entity) => new()
    {
        Id = entity.Id,
        Type = entity.Type,
        Title = entity.Title,
        Namespace = entity.Namespace,
        Assembly = entity.Members.FirstOrDefault(member => member.StartsWith("assembly:", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim(),
        Sources = entity.Sources,
        Members = entity.Members,
        CodeOwnership = CodeOwnershipClassifier.Classify(entity),
    };

    private static HashSet<string> ExpandRelatedIds(string entityId, IReadOnlyList<Relation> relations, int depth)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityId };
        var frontier = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entityId };
        for (var level = 0; level < depth; level++)
        {
            var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relation in relations)
            {
                if (frontier.Contains(relation.Source) && visited.Add(relation.Target))
                {
                    next.Add(relation.Target);
                }

                if (frontier.Contains(relation.Target) && visited.Add(relation.Source))
                {
                    next.Add(relation.Source);
                }
            }

            if (next.Count == 0)
            {
                break;
            }

            frontier = next;
        }

        return visited;
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static int NormalizeLimit(int value, int defaultLimit) =>
        value <= 0 ? defaultLimit : Math.Min(value, 1_000);

    private static List<Relation> OrderRelations(IEnumerable<Relation> relations) => relations
        .OrderBy(relation => relation.Source, StringComparer.Ordinal)
        .ThenBy(relation => relation.Target, StringComparer.Ordinal)
        .ThenBy(relation => relation.Type)
        .ToList();

    private readonly record struct RelationKey(string Source, string Target, RelationType Type)
    {
        public static IEqualityComparer<RelationKey> Comparer { get; } = new RelationKeyComparer();

        public static RelationKey From(Relation relation) => new(relation.Source, relation.Target, relation.Type);

        private sealed class RelationKeyComparer : IEqualityComparer<RelationKey>
        {
            public bool Equals(RelationKey x, RelationKey y) =>
                string.Equals(x.Source, y.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Target, y.Target, StringComparison.OrdinalIgnoreCase)
                && x.Type == y.Type;

            public int GetHashCode(RelationKey obj) => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Source),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Target),
                obj.Type);
        }
    }

    private static string ValidateWikiRoot(WikiNavigationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wikiRoot = Path.GetFullPath(options.WikiRoot);
        if (!Directory.Exists(wikiRoot))
        {
            throw new DirectoryNotFoundException($"Wiki root does not exist: {wikiRoot}");
        }

        return wikiRoot;
    }

    private static bool IsWithin(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return relative == "." || (!Path.IsPathRooted(relative) && !relative.StartsWith("..", StringComparison.Ordinal));
    }
}
