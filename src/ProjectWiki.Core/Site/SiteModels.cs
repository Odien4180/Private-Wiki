namespace ProjectWiki.Core.Site;

public sealed class WikiBuildOptions
{
    public required string WikiRoot { get; init; }
}

public sealed class WikiBuildResult
{
    public required string WikiRoot { get; init; }

    public required string SiteRoot { get; init; }

    public required int DocumentCount { get; init; }

    public required int EntityPageCount { get; init; }

    public required int SearchEntryCount { get; init; }

    public required int HealthIssueCount { get; init; }
}

public sealed class WikiServeOptions
{
    public required string WikiRoot { get; init; }

    public int Port { get; init; } = 8080;
}

public sealed class WikiServeResult
{
    public required string Url { get; init; }

    public required WikiBuildResult Build { get; init; }
}
