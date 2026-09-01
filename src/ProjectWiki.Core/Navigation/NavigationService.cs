using ProjectWiki.Core.Persistence;
using ProjectWiki.Core.Scanning;

namespace ProjectWiki.Core.Navigation;

public sealed class NavigationService
{
    private static readonly string[] ManualSectionPlaceholderPrefixes =
    [
        "Developer Notes",
        "사용자가 직접 작성",
    ];

    public WikiNavigationResult Build(string wikiRoot)
    {
        var store = new NavigationStore();
        var data = store.Load(wikiRoot);
        var documents = ReadDocuments(wikiRoot);
        var links = documents.Links;
        var resolver = new NavigationResolver(data);
        var backlinks = BuildBacklinks(links, resolver);
        var validation = Validate(data, links, resolver, backlinks, validatePersistedBacklinks: false);
        store.WriteBacklinks(wikiRoot, backlinks);

        return CreateResult(documents.DocumentCount, links, resolver, backlinks, validation);
    }

    public NavigationValidationResult Validate(string wikiRoot) => Validate(wikiRoot, requireDocuments: false, minCoverage: 0);

    public NavigationValidationResult Validate(string wikiRoot, bool requireDocuments, double minCoverage)
    {
        var data = new NavigationStore().Load(wikiRoot);
        var documents = ReadDocuments(wikiRoot);
        var links = documents.Links;
        var resolver = new NavigationResolver(data);
        var expectedBacklinks = BuildBacklinks(links, resolver);
        var structure = Validate(data, links, resolver, expectedBacklinks, validatePersistedBacklinks: true);
        if (!requireDocuments && minCoverage <= 0)
        {
            return structure;
        }

        var qualityIssues = ValidateQuality(wikiRoot, data, resolver, requireDocuments, minCoverage);
        return new NavigationValidationResult
        {
            Issues = structure.Issues,
            StructureIssues = structure.Issues,
            QualityIssues = qualityIssues,
        };
    }

    private static WikiNavigationResult CreateResult(
        int documentCount,
        IReadOnlyList<WikiLink> links,
        NavigationResolver resolver,
        BacklinkIndex backlinks,
        NavigationValidationResult validation) => new()
        {
            DocumentCount = documentCount,
            WikiLinkCount = links.Count,
            ResolvedWikiLinkCount = links.Count(link =>
                !link.IsMalformed && resolver.Resolve(link.Target).Status == NavigationResolutionStatus.Resolved),
            BacklinkCount = backlinks.Backlinks.Sum(backlink => backlink.References.Count),
            Validation = validation,
        };

    private static (int DocumentCount, List<WikiLink> Links) ReadDocuments(string wikiRoot)
    {
        var documentsRoot = Path.Combine(Path.GetFullPath(wikiRoot), "documents");
        if (!Directory.Exists(documentsRoot))
        {
            return (0, new List<WikiLink>());
        }

        var documentPaths = Directory.EnumerateFiles(documentsRoot, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return (
            documentPaths.Count,
            documentPaths.SelectMany(path => WikiLinkParser.Parse(
                ToDocumentPath(documentsRoot, path),
                File.ReadAllText(path))).ToList());
    }

    private static BacklinkIndex BuildBacklinks(
        IEnumerable<WikiLink> links,
        NavigationResolver resolver)
    {
        var references = new Dictionary<string, List<BacklinkReference>>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            if (link.IsMalformed)
            {
                continue;
            }

            var resolution = resolver.Resolve(link.Target);
            if (resolution.Status != NavigationResolutionStatus.Resolved)
            {
                continue;
            }

            if (!references.TryGetValue(resolution.EntityId!, out var targetReferences))
            {
                targetReferences = new List<BacklinkReference>();
                references[resolution.EntityId!] = targetReferences;
            }

            targetReferences.Add(new BacklinkReference
            {
                DocumentPath = link.DocumentPath,
                Line = link.Line,
                Column = link.Column,
                LinkTarget = link.Target,
            });
        }

        return new BacklinkIndex
        {
            Backlinks = references
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new BacklinkEntry
                {
                    Target = pair.Key,
                    References = pair.Value
                        .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
                        .ThenBy(reference => reference.Line)
                        .ThenBy(reference => reference.Column)
                        .ThenBy(reference => reference.LinkTarget, StringComparer.Ordinal)
                        .ToList(),
                })
                .ToList(),
        };
    }

    private static NavigationValidationResult Validate(
        NavigationData data,
        IReadOnlyList<WikiLink> links,
        NavigationResolver resolver,
        BacklinkIndex expectedBacklinks,
        bool validatePersistedBacklinks)
    {
        var issues = new List<NavigationValidationIssue>();
        ValidateEntities(data, issues);
        ValidateAliases(data, resolver, issues);
        ValidateRedirects(data, resolver, issues);
        ValidateLinks(links, resolver, issues);
        if (validatePersistedBacklinks)
        {
            ValidateBacklinks(data, expectedBacklinks, issues);
        }

        return new NavigationValidationResult
        {
            Issues = issues
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.DocumentPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Line)
                .ThenBy(issue => issue.Column)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToList(),
            StructureIssues = issues
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.DocumentPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Line)
                .ThenBy(issue => issue.Column)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private static List<NavigationValidationIssue> ValidateQuality(
        string wikiRoot,
        NavigationData data,
        NavigationResolver resolver,
        bool requireDocuments,
        double minCoverage)
    {
        var documentsRoot = Path.Combine(Path.GetFullPath(wikiRoot), "documents");
        var docs = Directory.Exists(documentsRoot)
            ? Directory.EnumerateFiles(documentsRoot, "*.md", SearchOption.AllDirectories)
                .Select(path => new DocumentContent(ToDocumentPath(documentsRoot, path), File.ReadAllText(path)))
                .OrderBy(doc => doc.Path, StringComparer.Ordinal)
                .ToList()
            : new List<DocumentContent>();
        var issues = new List<NavigationValidationIssue>();
        if (requireDocuments && !docs.Any(doc => doc.Path.StartsWith("systems/", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(issues, "no_system_documents", "At least one system document is required.");
        }

        if (requireDocuments && !docs.Any(doc => doc.Path.StartsWith("features/", StringComparison.OrdinalIgnoreCase)))
        {
            AddIssue(issues, "no_feature_documents", "At least one feature document is required.");
        }

        if (requireDocuments && !docs.Any(doc => doc.Path.StartsWith("architecture/", StringComparison.OrdinalIgnoreCase) && HasAgentOrManualProse(doc.Content)))
        {
            AddIssue(issues, "no_architecture_prose", "Architecture documents must contain agent-authored or manual explanatory prose.");
        }

        foreach (var doc in docs)
        {
            foreach (var block in ExtractAgentBlocks(doc.Content))
            {
                if (string.IsNullOrWhiteSpace(block))
                {
                    issues.Add(new NavigationValidationIssue
                    {
                        Code = "empty_agent_block",
                        Severity = NavigationIssueSeverity.Error,
                        Message = "AGENT blocks must contain source-grounded prose or be omitted.",
                        DocumentPath = doc.Path,
                    });
                }
            }

            if (IsQualityDocument(doc.Path) && HasAgentBlock(doc.Content) && !ContainsSourceEvidence(doc.Content))
            {
                issues.Add(new NavigationValidationIssue
                {
                    Code = "missing_source_evidence",
                    Severity = NavigationIssueSeverity.Error,
                    Message = "Agent-authored documents must cite real source paths or evidence.",
                    DocumentPath = doc.Path,
                });
            }

            foreach (var link in WikiLinkParser.Parse(doc.Path, doc.Content)
                         .Where(link => !link.IsMalformed && resolver.Resolve(link.Target).Status == NavigationResolutionStatus.Broken))
            {
                issues.Add(new NavigationValidationIssue
                {
                    Code = "stale_agent_document",
                    Severity = NavigationIssueSeverity.Error,
                    Message = $"Document references deleted or unknown entity '{link.Target}'.",
                    DocumentPath = doc.Path,
                    Line = link.Line,
                    Column = link.Column,
                });
            }
        }

        var firstPartyEntities = data.Entities.Entities
            .Where(entity => CodeOwnershipClassifier.Classify(entity) == "first_party" && entity.Sources.Count > 0)
            .ToList();
        var relationCounts = data.Entities.Entities
            .ToDictionary(entity => entity.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var relationsPath = Path.Combine(wikiRoot, "knowledge", "relations.json");
        if (File.Exists(relationsPath))
        {
            foreach (var relation in AtomicFile.ReadJson<ProjectWiki.Core.Engine.RelationCatalog>(relationsPath).Relations)
            {
                if (relationCounts.ContainsKey(relation.Source))
                {
                    relationCounts[relation.Source]++;
                }

                if (relationCounts.ContainsKey(relation.Target))
                {
                    relationCounts[relation.Target]++;
                }
            }
        }

        var importantFirstPartyEntities = firstPartyEntities
            .Where(entity => IsImportantEntity(entity, relationCounts.GetValueOrDefault(entity.Id)))
            .ToList();
        if (importantFirstPartyEntities.Count > 0 && minCoverage > 0)
        {
            var documented = importantFirstPartyEntities.Count(entity => IsEntityDocumented(entity, docs));
            var coverage = documented / (double)importantFirstPartyEntities.Count;
            if (coverage < minCoverage)
            {
                AddIssue(issues, "first_party_coverage_too_low", $"Important first-party documentation coverage {coverage:0.##} is below required {minCoverage:0.##}.");
            }
        }

        foreach (var entity in importantFirstPartyEntities.Where(entity => !IsEntityDocumented(entity, docs)))
        {
            AddIssue(issues, "undocumented_important_entity", $"Important entity '{entity.Id}' has graph references but no document coverage.");
        }

        var thirdPartyMentions = data.Entities.Entities
            .Where(entity => CodeOwnershipClassifier.Classify(entity) == "third_party")
            .Count(entity => IsEntityDocumented(entity, docs));
        var firstPartyMentions = firstPartyEntities.Count(entity => IsEntityDocumented(entity, docs));
        if (thirdPartyMentions > 0 && firstPartyMentions > 0 && thirdPartyMentions > firstPartyMentions)
        {
            AddIssue(issues, "third_party_noise_too_high", "Third-party documented entity mentions exceed first-party mentions.");
        }

        return issues
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.DocumentPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Line)
            .ToList();
    }

    private static void ValidateEntities(NavigationData data, List<NavigationValidationIssue> issues)
    {
        foreach (var duplicate in data.Entities.Entities
                     .GroupBy(entity => Normalize(entity.Id), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Key.Length > 0 && group.Count() > 1))
        {
            AddIssue(issues, "duplicate_entity_id", $"Entity id '{duplicate.First().Id}' is declared more than once.");
        }
    }

    private static void ValidateAliases(
        NavigationData data,
        NavigationResolver resolver,
        List<NavigationValidationIssue> issues)
    {
        foreach (var duplicate in data.Aliases.Aliases
                     .GroupBy(alias => Normalize(alias.Alias), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Key.Length > 0 && group.Count() > 1))
        {
            AddIssue(issues, "duplicate_alias", $"Alias '{duplicate.First().Alias}' is declared more than once.");
        }

        foreach (var alias in data.Aliases.Aliases)
        {
            var normalizedAlias = Normalize(alias.Alias);
            if (normalizedAlias.Length == 0)
            {
                AddIssue(issues, "invalid_alias", "Aliases must not be empty.");
                continue;
            }

            if (alias.Targets.Count == 0)
            {
                AddIssue(issues, "invalid_alias", $"Alias '{alias.Alias}' must reference at least one entity.");
            }

            foreach (var duplicateTarget in alias.Targets
                         .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Key.Length > 0 && group.Count() > 1))
            {
                AddIssue(issues, "invalid_alias", $"Alias '{alias.Alias}' references entity '{duplicateTarget.First()}' more than once.");
            }

            foreach (var target in alias.Targets.Where(target => !resolver.HasEntity(target)))
            {
                AddIssue(issues, "broken_alias", $"Alias '{alias.Alias}' references unknown entity '{target}'.");
            }
        }
    }

    private static void ValidateRedirects(
        NavigationData data,
        NavigationResolver resolver,
        List<NavigationValidationIssue> issues)
    {
        foreach (var duplicate in data.Redirects.Redirects
                     .GroupBy(redirect => Normalize(redirect.From), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Key.Length > 0 && group.Count() > 1))
        {
            AddIssue(issues, "duplicate_redirect", $"Redirect '{duplicate.First().From}' is declared more than once.");
        }

        foreach (var redirect in data.Redirects.Redirects)
        {
            if (Normalize(redirect.From).Length == 0 || Normalize(redirect.To).Length == 0)
            {
                AddIssue(issues, "invalid_redirect", "Redirect sources and targets must not be empty.");
                continue;
            }

            if (resolver.HasEntity(redirect.From))
            {
                AddIssue(issues, "invalid_redirect", $"Redirect '{redirect.From}' shadows an entity id.");
                continue;
            }

            var resolution = resolver.Resolve(redirect.From);
            switch (resolution.Status)
            {
                case NavigationResolutionStatus.Broken:
                    AddIssue(issues, "broken_redirect", $"Redirect '{redirect.From}' does not resolve to an entity.");
                    break;
                case NavigationResolutionStatus.Cycle:
                    AddIssue(issues, "redirect_cycle", $"Redirect '{redirect.From}' is part of a redirect cycle.");
                    break;
                case NavigationResolutionStatus.Ambiguous:
                    AddIssue(issues, "ambiguous_redirect", $"Redirect '{redirect.From}' resolves to multiple entities.");
                    break;
            }
        }
    }

    private static void ValidateLinks(
        IEnumerable<WikiLink> links,
        NavigationResolver resolver,
        List<NavigationValidationIssue> issues)
    {
        foreach (var link in links)
        {
            if (link.IsMalformed)
            {
                AddIssue(issues, "malformed_wiki_link", "Wiki links must have a non-empty target and closing brackets.", link);
                continue;
            }

            var resolution = resolver.Resolve(link.Target);
            switch (resolution.Status)
            {
                case NavigationResolutionStatus.Broken:
                case NavigationResolutionStatus.Cycle:
                    AddIssue(issues, "broken_wiki_link", $"Wiki link '{link.Target}' does not resolve to an entity.", link);
                    break;
                case NavigationResolutionStatus.Ambiguous:
                    AddIssue(issues, "ambiguous_wiki_link", $"Wiki link '{link.Target}' resolves to multiple entities.", link);
                    break;
            }
        }
    }

    private static void ValidateBacklinks(
        NavigationData data,
        BacklinkIndex expectedBacklinks,
        List<NavigationValidationIssue> issues)
    {
        if (!data.HasPersistedBacklinks)
        {
            AddIssue(issues, "missing_backlink_index", "knowledge/backlinks.json has not been generated.");
            return;
        }

        var expected = CountReferences(expectedBacklinks);
        var persisted = CountReferences(data.Backlinks);
        foreach (var missing in expected.Where(pair => persisted.GetValueOrDefault(pair.Key) < pair.Value))
        {
            AddIssue(issues, "missing_backlink", $"Backlink '{missing.Key}' is missing from the persisted index.");
        }

        foreach (var unexpected in persisted.Where(pair => expected.GetValueOrDefault(pair.Key) < pair.Value))
        {
            AddIssue(issues, "invalid_backlink", $"Backlink '{unexpected.Key}' does not match a resolved wiki link.");
        }
    }

    private static Dictionary<BacklinkKey, int> CountReferences(BacklinkIndex index)
    {
        var references = new Dictionary<BacklinkKey, int>();
        foreach (var backlink in index.Backlinks)
        {
            foreach (var reference in backlink.References)
            {
                var key = new BacklinkKey(
                    backlink.Target,
                    reference.DocumentPath,
                    reference.Line,
                    reference.Column,
                    reference.LinkTarget);
                references[key] = references.GetValueOrDefault(key) + 1;
            }
        }

        return references;
    }

    private static bool HasAgentOrManualProse(string content)
    {
        if (ExtractAgentBlocks(content).Any(block => !string.IsNullOrWhiteSpace(block)))
        {
            return true;
        }

        var manual = RemoveMarkedBlocks(content)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0
                && !line.StartsWith('#')
                && !ManualSectionPlaceholderPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        return manual.Any();
    }

    private static bool HasAgentBlock(string content) =>
        content.Contains("<!-- AGENT:", StringComparison.Ordinal);

    private static IEnumerable<string> ExtractAgentBlocks(string content)
    {
        const string startPrefix = "<!-- AGENT:";
        var index = 0;
        while (index < content.Length)
        {
            var start = content.IndexOf(startPrefix, index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            var startEnd = content.IndexOf("-->", start, StringComparison.Ordinal);
            if (startEnd < 0)
            {
                yield break;
            }

            var name = content[(start + startPrefix.Length)..startEnd].Replace(":START", string.Empty, StringComparison.Ordinal).Trim();
            var endMarker = $"<!-- AGENT:{name}:END -->";
            var end = content.IndexOf(endMarker, startEnd + 3, StringComparison.Ordinal);
            if (end < 0)
            {
                yield break;
            }

            yield return content[(startEnd + 3)..end].Trim();
            index = end + endMarker.Length;
        }
    }

    private static string RemoveMarkedBlocks(string content)
    {
        var result = content;
        foreach (var prefix in new[] { "AUTO", "AGENT" })
        {
            var index = 0;
            while (index < result.Length)
            {
                var startPrefix = $"<!-- {prefix}:";
                var start = result.IndexOf(startPrefix, index, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                var startEnd = result.IndexOf("-->", start, StringComparison.Ordinal);
                if (startEnd < 0)
                {
                    break;
                }

                var name = result[(start + startPrefix.Length)..startEnd].Replace(":START", string.Empty, StringComparison.Ordinal).Trim();
                var endMarker = $"<!-- {prefix}:{name}:END -->";
                var end = result.IndexOf(endMarker, startEnd + 3, StringComparison.Ordinal);
                if (end < 0)
                {
                    break;
                }

                result = result[..start] + result[(end + endMarker.Length)..];
                index = start;
            }
        }

        return result;
    }

    private static bool ContainsSourceEvidence(string content) =>
        content.Contains(".cs", StringComparison.OrdinalIgnoreCase)
        || content.Contains(".unity", StringComparison.OrdinalIgnoreCase)
        || content.Contains(".prefab", StringComparison.OrdinalIgnoreCase)
        || content.Contains(".asset", StringComparison.OrdinalIgnoreCase)
        || content.Contains("Assets/", StringComparison.OrdinalIgnoreCase)
        || content.Contains("Packages/", StringComparison.OrdinalIgnoreCase);

    private static bool IsQualityDocument(string path) =>
        path.StartsWith("architecture/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("systems/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("features/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("classes/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("scenes/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("packages/", StringComparison.OrdinalIgnoreCase);

    private static bool IsEntityDocumented(ProjectWiki.Core.Model.Entity entity, IEnumerable<DocumentContent> documents) =>
        documents.Any(doc =>
            doc.Content.Contains($"[[{entity.Id}", StringComparison.OrdinalIgnoreCase)
            || doc.Content.Contains(entity.Title, StringComparison.OrdinalIgnoreCase)
            || entity.Sources.Any(source => doc.Content.Contains(source, StringComparison.OrdinalIgnoreCase)));

    private static bool IsImportantEntity(ProjectWiki.Core.Model.Entity entity, int relationCount) =>
        relationCount >= 2
        || entity.Type is ProjectWiki.Core.Model.EntityType.Scene
            or ProjectWiki.Core.Model.EntityType.Prefab
            or ProjectWiki.Core.Model.EntityType.Component
            or ProjectWiki.Core.Model.EntityType.Service
            or ProjectWiki.Core.Model.EntityType.Manager;

    private static void AddIssue(List<NavigationValidationIssue> issues, string code, string message, WikiLink? link = null) =>
        issues.Add(new NavigationValidationIssue
        {
            Code = code,
            Severity = NavigationIssueSeverity.Error,
            Message = message,
            DocumentPath = link?.DocumentPath,
            Line = link?.Line,
            Column = link?.Column,
        });

    private static string ToDocumentPath(string documentsRoot, string path) =>
        Path.GetRelativePath(documentsRoot, path).Replace('\\', '/');

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private readonly record struct BacklinkKey(
        string Target,
        string DocumentPath,
        int Line,
        int Column,
        string LinkTarget);

    private readonly record struct DocumentContent(string Path, string Content);
}

internal sealed class NavigationResolver
{
    private readonly Dictionary<string, List<string>> _entityIds;
    private readonly Dictionary<string, List<AliasEntry>> _aliases;
    private readonly Dictionary<string, List<RedirectEntry>> _redirects;

    public NavigationResolver(NavigationData data)
    {
        _entityIds = data.Entities.Entities
            .GroupBy(entity => Normalize(entity.Id), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entity => entity.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);
        _aliases = data.Aliases.Aliases
            .GroupBy(alias => Normalize(alias.Alias), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        _redirects = data.Redirects.Redirects
            .GroupBy(redirect => Normalize(redirect.From), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasEntity(string value) =>
        _entityIds.TryGetValue(Normalize(value), out var ids) && ids.Count == 1;

    public NavigationResolution Resolve(string value)
    {
        var current = Normalize(value);
        if (current.Length == 0)
        {
            return NavigationResolution.Broken;
        }

        var visitedRedirects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            if (_entityIds.TryGetValue(current, out var entityIds))
            {
                return entityIds.Count == 1
                    ? NavigationResolution.ForEntity(entityIds[0])
                    : NavigationResolution.Ambiguous;
            }

            if (_redirects.TryGetValue(current, out var redirects))
            {
                if (redirects.Count != 1 || !visitedRedirects.Add(current))
                {
                    return redirects.Count == 1 ? NavigationResolution.Cycle : NavigationResolution.Ambiguous;
                }

                current = Normalize(redirects[0].To);
                if (current.Length == 0)
                {
                    return NavigationResolution.Broken;
                }

                continue;
            }

            if (!_aliases.TryGetValue(current, out var aliases))
            {
                return NavigationResolution.Broken;
            }

            var targets = aliases.SelectMany(alias => alias.Targets)
                .Select(Normalize)
                .ToList();
            if (targets.Count == 0 || targets.Any(target => !_entityIds.TryGetValue(target, out var ids) || ids.Count != 1))
            {
                return NavigationResolution.Broken;
            }

            var entityTargets = targets
                .Select(target => _entityIds[target][0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return entityTargets.Count == 1
                ? NavigationResolution.ForEntity(entityTargets[0])
                : NavigationResolution.Ambiguous;
        }
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}

internal sealed class NavigationResolution
{
    public static NavigationResolution Broken { get; } = new(NavigationResolutionStatus.Broken, null);

    public static NavigationResolution Ambiguous { get; } = new(NavigationResolutionStatus.Ambiguous, null);

    public static NavigationResolution Cycle { get; } = new(NavigationResolutionStatus.Cycle, null);

    private NavigationResolution(NavigationResolutionStatus status, string? entityId)
    {
        Status = status;
        EntityId = entityId;
    }

    public NavigationResolutionStatus Status { get; }

    public string? EntityId { get; }

    public static NavigationResolution ForEntity(string entityId) =>
        new(NavigationResolutionStatus.Resolved, entityId);
}

internal enum NavigationResolutionStatus
{
    Resolved,
    Broken,
    Ambiguous,
    Cycle,
}
