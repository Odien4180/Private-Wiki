namespace ProjectWiki.Core.Config;

public sealed class ProjectConfig
{
    public required string Root { get; init; }

    public ProjectType Type { get; init; } = ProjectType.Generic;
}

public sealed class WikiMeta
{
    public required string Title { get; init; }

    public string Language { get; init; } = "ko";
}

public sealed class AnalysisConfig
{
    public bool Git { get; init; } = true;

    public bool HashFallback { get; init; } = true;
}

/// <summary>The persisted <c>wiki.config.json</c> document at the root of a wiki.</summary>
public sealed class WikiConfig
{
    public int Version { get; init; } = 1;

    public required ProjectConfig Project { get; init; }

    public required WikiMeta Wiki { get; init; }

    public AnalysisConfig Analysis { get; init; } = new();

    public List<string> Exclude { get; init; } = new();
}
