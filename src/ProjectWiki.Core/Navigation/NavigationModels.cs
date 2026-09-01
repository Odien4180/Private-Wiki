using ProjectWiki.Core.Model;

namespace ProjectWiki.Core.Navigation;

public sealed class EntityCatalog
{
    public List<Entity> Entities { get; init; } = new();
}

public sealed class AliasIndex
{
    public List<AliasEntry> Aliases { get; init; } = new();
}

public sealed class AliasEntry
{
    public required string Alias { get; init; }

    public List<string> Targets { get; init; } = new();
}

public sealed class RedirectIndex
{
    public List<RedirectEntry> Redirects { get; init; } = new();
}

public sealed class RedirectEntry
{
    public required string From { get; init; }

    public required string To { get; init; }
}

public sealed class BacklinkIndex
{
    public List<BacklinkEntry> Backlinks { get; init; } = new();
}

public sealed class BacklinkEntry
{
    public required string Target { get; init; }

    public List<BacklinkReference> References { get; init; } = new();
}

public sealed class BacklinkReference
{
    public required string DocumentPath { get; init; }

    public required int Line { get; init; }

    public required int Column { get; init; }

    public required string LinkTarget { get; init; }
}

public sealed class WikiLink
{
    public required string DocumentPath { get; init; }

    public required int Line { get; init; }

    public required int Column { get; init; }

    public required string Target { get; init; }

    public string? DisplayText { get; init; }

    public bool IsMalformed { get; init; }
}

public sealed class NavigationValidationResult
{
    public List<NavigationValidationIssue> Issues { get; init; } = new();

    public List<NavigationValidationIssue> StructureIssues { get; init; } = new();

    public List<NavigationValidationIssue> QualityIssues { get; init; } = new();

    public bool IsValid => Issues.Count == 0 && QualityIssues.Count == 0;
}

public sealed class NavigationValidationIssue
{
    public required string Code { get; init; }

    public required NavigationIssueSeverity Severity { get; init; }

    public required string Message { get; init; }

    public string? DocumentPath { get; init; }

    public int? Line { get; init; }

    public int? Column { get; init; }
}

public enum NavigationIssueSeverity
{
    Error,
}

public sealed class WikiNavigationOptions
{
    public required string WikiRoot { get; init; }

    public bool RequireDocuments { get; init; }

    public double MinCoverage { get; init; }
}

public sealed class WikiNavigationResult
{
    public required int DocumentCount { get; init; }

    public required int WikiLinkCount { get; init; }

    public required int ResolvedWikiLinkCount { get; init; }

    public required int BacklinkCount { get; init; }

    public required NavigationValidationResult Validation { get; init; }
}
