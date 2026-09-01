using System.Text.Json;
using System.Text.RegularExpressions;
using ProjectWiki.Core.Model;
using ProjectWiki.Core.Scanning;

namespace ProjectWiki.Core.Analysis;

/// <summary>
/// Extracts direct, textual facts from Unity project files. Serialized GUID
/// references are emitted only when their target is uniquely declared by a
/// scanned <c>.meta</c> file.
/// </summary>
public sealed class UnityAnalyzer
{
    private const string ManifestPath = "Packages/manifest.json";
    private static readonly Regex MetaGuidPattern = new(
        @"^\s*guid\s*:\s*(?<guid>[0-9a-fA-F]{32})\s*(?:#.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SerializedGuidPattern = new(
        @"(?:^\s*|[,{]\s*)guid\s*:\s*(?<guid>[0-9a-fA-F]{32})(?=\s*(?:[,}#]|$))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AssemblyGuidReferencePattern = new(
        @"^GUID:(?<guid>[0-9a-fA-F]{32})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AnalysisResult Analyze(
        string projectRoot,
        IReadOnlyList<ScannedFile> scannedFiles,
        IEnumerable<string>? reservedEntityIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(scannedFiles);

        var result = new AnalysisResult();
        var knownPaths = scannedFiles.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var usedIds = new HashSet<string>(reservedEntityIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var asmdefFacts = scannedFiles
            .Where(file => HasExtension(file, ".asmdef"))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToDictionary(file => file.Path, file => ReadAsmdef(projectRoot, file.Path), StringComparer.Ordinal);
        var metaAssets = ReadMetaAssets(projectRoot, scannedFiles);
        var assetEntitiesByPath = new Dictionary<string, Entity>(StringComparer.Ordinal);

        foreach (var metaAsset in metaAssets)
        {
            asmdefFacts.TryGetValue(metaAsset.AssetPath, out var asmdef);
            var entity = CreateAssetEntity(metaAsset, asmdef, knownPaths, usedIds);
            result.Entities.Add(entity);
            assetEntitiesByPath[metaAsset.AssetPath] = entity;
            metaAsset.Entity = entity;
        }

        foreach (var (path, asmdef) in asmdefFacts)
        {
            if (assetEntitiesByPath.ContainsKey(path) || !asmdef.IsValid)
            {
                continue;
            }

            var entity = CreateAsmdefEntity(path, asmdef, usedIds);
            result.Entities.Add(entity);
            assetEntitiesByPath[path] = entity;
        }

        var asmdefEntities = asmdefFacts
            .Where(pair => pair.Value.IsValid && assetEntitiesByPath.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => assetEntitiesByPath[pair.Key], StringComparer.Ordinal);
        AddAsmdefRelations(result, asmdefFacts, asmdefEntities, metaAssets);
        AddSerializedGuidRelations(result, projectRoot, scannedFiles, assetEntitiesByPath, metaAssets);
        AddManifestPackages(result, projectRoot, scannedFiles, usedIds);

        return new AnalysisResult
        {
            Entities = result.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToList(),
            Relations = OrderRelations(result.Relations),
        };
    }

    /// <summary>Identifies graph records owned by this deterministic analyzer.</summary>
    public static bool IsManaged(Entity entity) =>
        entity.Symbols.Any(symbol => symbol.StartsWith("unity:", StringComparison.Ordinal));

    /// <summary>Identifies relations whose source is an entity emitted by this analyzer.</summary>
    public static bool IsManaged(Relation relation) =>
        relation.Source.StartsWith("unity-", StringComparison.Ordinal);

    private static List<MetaAsset> ReadMetaAssets(string projectRoot, IReadOnlyList<ScannedFile> scannedFiles) =>
        scannedFiles
            .Where(file => HasExtension(file, ".meta"))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => new MetaAsset(file.Path, file.Path[..^".meta".Length], ReadMetaGuid(projectRoot, file.Path)))
            .Where(asset => asset.Guid is not null)
            .Select(asset => asset with { Guid = asset.Guid!.ToLowerInvariant() })
            .ToList();

    private static string? ReadMetaGuid(string projectRoot, string metaPath)
    {
        var guids = File.ReadLines(ToFullPath(projectRoot, metaPath))
            .Select(line => MetaGuidPattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["guid"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return guids.Count == 1 ? guids[0] : null;
    }

    private static AsmdefFact ReadAsmdef(string projectRoot, string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ToFullPath(projectRoot, path)));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new AsmdefFact(false, null, Array.Empty<string>());
            }

            var name = document.RootElement.TryGetProperty("name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;
            var references = document.RootElement.TryGetProperty("references", out var referencesElement)
                && referencesElement.ValueKind == JsonValueKind.Array
                ? referencesElement.EnumerateArray()
                    .Where(reference => reference.ValueKind == JsonValueKind.String)
                    .Select(reference => reference.GetString())
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Cast<string>()
                    .OrderBy(reference => reference, StringComparer.Ordinal)
                    .ToList()
                : new List<string>();
            return new AsmdefFact(true, string.IsNullOrWhiteSpace(name) ? null : name, references);
        }
        catch (JsonException)
        {
            return new AsmdefFact(false, null, Array.Empty<string>());
        }
    }

    private static Entity CreateAssetEntity(
        MetaAsset metaAsset,
        AsmdefFact? asmdef,
        IReadOnlySet<string> knownPaths,
        HashSet<string> usedIds)
    {
        var sourcePaths = new List<string>();
        if (knownPaths.Contains(metaAsset.AssetPath))
        {
            sourcePaths.Add(metaAsset.AssetPath);
        }

        sourcePaths.Add(metaAsset.MetaPath);
        var type = GetAssetEntityType(metaAsset.AssetPath);
        var title = asmdef?.Name ?? Path.GetFileNameWithoutExtension(metaAsset.AssetPath);
        var symbols = new List<string> { metaAsset.Guid!, $"unity:guid:{metaAsset.Guid}" };
        var members = new List<string>();
        if (asmdef?.IsValid == true)
        {
            symbols.Add($"unity:asmdef:{asmdef.Name ?? metaAsset.AssetPath}");
            if (asmdef.Name is not null)
            {
                members.Add($"assembly: {asmdef.Name}");
            }

            members.AddRange(asmdef.References.Select(reference => $"reference: {reference}"));
        }

        return new Entity
        {
            Id = EntityIdGenerator.MakeUnique($"unity-asset-{metaAsset.Guid}", null, usedIds),
            Type = type,
            Title = title,
            Aliases = new List<string> { metaAsset.AssetPath },
            Sources = sourcePaths.OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Symbols = symbols.OrderBy(symbol => symbol, StringComparer.Ordinal).ToList(),
            Members = members.OrderBy(member => member, StringComparer.Ordinal).ToList(),
        };
    }

    private static Entity CreateAsmdefEntity(string path, AsmdefFact fact, HashSet<string> usedIds)
    {
        var title = fact.Name ?? Path.GetFileNameWithoutExtension(path);
        var symbols = new List<string> { $"unity:asmdef:{fact.Name ?? path}" };
        if (fact.Name is not null)
        {
            symbols.Add(fact.Name);
        }

        var members = fact.Name is null
            ? new List<string>()
            : new List<string> { $"assembly: {fact.Name}" };
        members.AddRange(fact.References.Select(reference => $"reference: {reference}"));
        return new Entity
        {
            Id = EntityIdGenerator.MakeUnique($"unity-asmdef-{path}", null, usedIds),
            Type = EntityType.Config,
            Title = title,
            Aliases = new List<string> { path },
            Sources = new List<string> { path },
            Symbols = symbols.OrderBy(symbol => symbol, StringComparer.Ordinal).ToList(),
            Members = members.OrderBy(member => member, StringComparer.Ordinal).ToList(),
        };
    }

    private static EntityType GetAssetEntityType(string assetPath) =>
        Path.GetExtension(assetPath).ToLowerInvariant() switch
        {
            ".unity" => EntityType.Scene,
            ".prefab" => EntityType.Prefab,
            ".asset" => EntityType.Data,
            ".asmdef" => EntityType.Config,
            _ => EntityType.Asset,
        };

    private static void AddAsmdefRelations(
        AnalysisResult result,
        IReadOnlyDictionary<string, AsmdefFact> asmdefFacts,
        IReadOnlyDictionary<string, Entity> asmdefEntities,
        IReadOnlyList<MetaAsset> metaAssets)
    {
        var entitiesByAssemblyName = asmdefFacts
            .Where(pair => pair.Value.Name is not null && asmdefEntities.ContainsKey(pair.Key))
            .GroupBy(pair => pair.Value.Name!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => asmdefEntities[pair.Key]).ToList(),
                StringComparer.Ordinal);
        var entitiesByGuid = metaAssets
            .GroupBy(asset => asset.Guid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(asset => asset.Entity!).ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var (path, fact) in asmdefFacts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!asmdefEntities.TryGetValue(path, out var source))
            {
                continue;
            }

            foreach (var reference in fact.References)
            {
                Entity? target = null;
                var guidMatch = AssemblyGuidReferencePattern.Match(reference);
                if (guidMatch.Success
                    && entitiesByGuid.TryGetValue(guidMatch.Groups["guid"].Value, out var byGuid)
                    && byGuid.Count == 1
                    && byGuid[0].Type == EntityType.Config)
                {
                    target = byGuid[0];
                }
                else if (entitiesByAssemblyName.TryGetValue(reference, out var byName) && byName.Count == 1)
                {
                    target = byName[0];
                }

                if (target is not null)
                {
                    AddRelation(result, source.Id, target.Id, RelationType.DependsOn, path, null);
                }
            }
        }
    }

    private static void AddSerializedGuidRelations(
        AnalysisResult result,
        string projectRoot,
        IReadOnlyList<ScannedFile> scannedFiles,
        IReadOnlyDictionary<string, Entity> assetEntitiesByPath,
        IReadOnlyList<MetaAsset> metaAssets)
    {
        var entitiesByGuid = metaAssets
            .GroupBy(asset => asset.Guid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(asset => asset.Entity!).ToList(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var file in scannedFiles
                     .Where(file => HasExtension(file, ".unity") || HasExtension(file, ".prefab") || HasExtension(file, ".asset"))
                     .OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            if (!assetEntitiesByPath.TryGetValue(file.Path, out var source))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(ToFullPath(projectRoot, file.Path)))
            {
                lineNumber++;
                foreach (Match match in SerializedGuidPattern.Matches(line))
                {
                    var guid = match.Groups["guid"].Value;
                    if (entitiesByGuid.TryGetValue(guid, out var targets) && targets.Count == 1)
                    {
                        AddRelation(result, source.Id, targets[0].Id, RelationType.References, file.Path, lineNumber);
                    }
                }
            }
        }
    }

    private static void AddManifestPackages(
        AnalysisResult result,
        string projectRoot,
        IReadOnlyList<ScannedFile> scannedFiles,
        HashSet<string> usedIds)
    {
        if (!scannedFiles.Any(file => string.Equals(file.Path, ManifestPath, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ToFullPath(projectRoot, ManifestPath)));
            if (!document.RootElement.TryGetProperty("dependencies", out var dependencies)
                || dependencies.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var dependency in dependencies.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (dependency.Value.ValueKind != JsonValueKind.String || dependency.Value.GetString() is not { } version)
                {
                    continue;
                }

                result.Entities.Add(new Entity
                {
                    Id = EntityIdGenerator.MakeUnique($"unity-package-{dependency.Name}", null, usedIds),
                    Type = EntityType.Package,
                    Title = dependency.Name,
                    Sources = new List<string> { ManifestPath },
                    Symbols = new List<string> { dependency.Name, $"unity:package:{dependency.Name}" },
                    Members = new List<string> { $"version: {version}" },
                });
            }
        }
        catch (JsonException)
        {
            // An invalid manifest supplies no reliable package facts.
        }
    }

    private static void AddRelation(
        AnalysisResult result,
        string source,
        string target,
        RelationType type,
        string file,
        int? line)
    {
        var existing = result.Relations.FirstOrDefault(relation =>
            string.Equals(relation.Source, source, StringComparison.Ordinal)
            && string.Equals(relation.Target, target, StringComparison.Ordinal)
            && relation.Type == type);
        var evidence = new Evidence { File = file, StartLine = line, EndLine = line };
        if (existing is null)
        {
            result.Relations.Add(new Relation
            {
                Source = source,
                Target = target,
                Type = type,
                Confidence = Confidence.High,
                Evidence = new List<Evidence> { evidence },
            });
            return;
        }

        if (!existing.Evidence.Any(item =>
                string.Equals(item.File, file, StringComparison.Ordinal)
                && item.StartLine == line))
        {
            existing.Evidence.Add(evidence);
        }
    }

    private static List<Relation> OrderRelations(IEnumerable<Relation> relations) => relations
        .OrderBy(relation => relation.Source, StringComparer.Ordinal)
        .ThenBy(relation => relation.Target, StringComparer.Ordinal)
        .ThenBy(relation => relation.Type)
        .ToList();

    private static bool HasExtension(ScannedFile file, string extension) =>
        string.Equals(file.Extension, extension, StringComparison.OrdinalIgnoreCase);

    private static string ToFullPath(string projectRoot, string relativePath) =>
        Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private sealed record MetaAsset(string MetaPath, string AssetPath, string? Guid)
    {
        public Entity? Entity { get; set; }
    }

    private sealed record AsmdefFact(bool IsValid, string? Name, IReadOnlyList<string> References);
}
