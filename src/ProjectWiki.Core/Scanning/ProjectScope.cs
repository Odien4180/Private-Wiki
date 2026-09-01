using ProjectWiki.Core.Config;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Scanning;

public sealed class ScopeFileRecord
{
    public required string Path { get; init; }

    public required string Classification { get; init; }

    public string? MatchedPattern { get; init; }

    public required string Reason { get; init; }
}

public sealed class AnalysisScopeReport
{
    public required ProjectType ProjectType { get; init; }

    public List<string> Include { get; init; } = new();

    public List<string> DefaultExclude { get; init; } = new();

    public List<string> UnityExclude { get; init; } = new();

    public List<string> UserExclude { get; init; } = new();

    public List<string> EffectiveExclude { get; init; } = new();

    public List<string> ReviewCandidates { get; init; } = new();

    public int TotalFileCount { get; init; }

    public int IncludedFileCount { get; init; }

    public int ExcludedFileCount { get; init; }

    public List<ScopeFileRecord> ExcludedFiles { get; init; } = new();

    public List<ScopeFileRecord> CandidateFiles { get; init; } = new();
}

public static class UnityExclusionProfile
{
    public static readonly IReadOnlyList<string> AutomaticExclusions = new[]
    {
        "Assets/AmplifyShaderEditor/**",
        "Assets/AmplifyShaderPack/**",
        "Assets/NiloToonURP/**",
        "Assets/Packages/**",
        "Assets/TextMesh Pro/**",
    };

    public static readonly IReadOnlyList<string> ReviewCandidates = new[]
    {
        "Assets/Plugins/**",
    };
}

public sealed class ProjectScopeAnalyzer
{
    public AnalysisScopeReport Analyze(
        string projectRoot,
        ProjectType projectType,
        IEnumerable<string>? userExclusions = null,
        IEnumerable<string>? includePatterns = null)
    {
        var normalizedRoot = Path.GetFullPath(projectRoot);
        var userExcludeList = NormalizePatterns(userExclusions);
        var includeList = NormalizePatterns(includePatterns);
        var unityExclude = projectType == ProjectType.Unity
            ? UnityExclusionProfile.AutomaticExclusions.ToList()
            : new List<string>();
        var reviewCandidates = projectType == ProjectType.Unity
            ? UnityExclusionProfile.ReviewCandidates.ToList()
            : new List<string>();
        var effectiveExclusions = DefaultExclusions.Patterns
            .Concat(unityExclude)
            .Concat(userExcludeList)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var excluded = new List<ScopeFileRecord>();
        var candidates = new List<ScopeFileRecord>();
        var includedCount = 0;
        var totalCount = 0;

        foreach (var file in Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories))
        {
            totalCount++;
            var relative = Path.GetRelativePath(normalizedRoot, file).Replace('\\', '/');
            if (TryMatch(relative, effectiveExclusions, out var matchedExclude))
            {
                excluded.Add(new ScopeFileRecord
                {
                    Path = relative,
                    Classification = ClassifyExcluded(matchedExclude!, unityExclude, userExcludeList),
                    MatchedPattern = matchedExclude,
                    Reason = "excluded",
                });
                continue;
            }

            if (includeList.Count > 0 && !GlobMatcher.IsMatchAny(relative, includeList))
            {
                excluded.Add(new ScopeFileRecord
                {
                    Path = relative,
                    Classification = "outside_include",
                    MatchedPattern = string.Join(", ", includeList),
                    Reason = "outside_include",
                });
                continue;
            }

            includedCount++;
            if (TryMatch(relative, reviewCandidates, out var candidatePattern))
            {
                candidates.Add(new ScopeFileRecord
                {
                    Path = relative,
                    Classification = "review_candidate",
                    MatchedPattern = candidatePattern,
                    Reason = "mixed_project_code_possible",
                });
            }
        }

        return new AnalysisScopeReport
        {
            ProjectType = projectType,
            Include = includeList,
            DefaultExclude = DefaultExclusions.Patterns.ToList(),
            UnityExclude = unityExclude,
            UserExclude = userExcludeList,
            EffectiveExclude = effectiveExclusions,
            ReviewCandidates = reviewCandidates,
            TotalFileCount = totalCount,
            IncludedFileCount = includedCount,
            ExcludedFileCount = excluded.Count,
            ExcludedFiles = excluded.OrderBy(file => file.Path, StringComparer.Ordinal).ToList(),
            CandidateFiles = candidates.OrderBy(file => file.Path, StringComparer.Ordinal).ToList(),
        };
    }

    public void WriteReport(string wikiRoot, AnalysisScopeReport report)
    {
        var reportsDir = Path.Combine(wikiRoot, "reports");
        Directory.CreateDirectory(reportsDir);
        AtomicFile.WriteJson(Path.Combine(reportsDir, "analysis-scope.json"), report);
    }

    private static List<string> NormalizePatterns(IEnumerable<string>? patterns) => (patterns ?? Array.Empty<string>())
        .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
        .Select(pattern => pattern.Replace('\\', '/').Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool TryMatch(string relativePath, IEnumerable<string> patterns, out string? matchedPattern)
    {
        foreach (var pattern in patterns)
        {
            if (GlobMatcher.IsMatch(relativePath, pattern))
            {
                matchedPattern = pattern;
                return true;
            }
        }

        matchedPattern = null;
        return false;
    }

    private static string ClassifyExcluded(string pattern, IReadOnlyList<string> unityExclude, IReadOnlyList<string> userExclude)
    {
        if (DefaultExclusions.Patterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
        {
            return "default_excluded";
        }

        if (unityExclude.Contains(pattern, StringComparer.OrdinalIgnoreCase))
        {
            return "third_party_unity_asset";
        }

        return userExclude.Contains(pattern, StringComparer.OrdinalIgnoreCase)
            ? "user_excluded"
            : "excluded";
    }
}
