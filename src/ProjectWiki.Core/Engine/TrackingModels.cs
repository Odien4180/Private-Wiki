namespace ProjectWiki.Core.Engine;

public sealed class TrackedFileRecord
{
    public required string Path { get; init; }

    public required string Extension { get; init; }

    public required long Size { get; init; }

    public required DateTime ModifiedUtc { get; init; }

    public required string Category { get; init; }
}

public sealed class FilesTracking
{
    public List<TrackedFileRecord> Files { get; init; } = new();
}

public sealed class HashesTracking
{
    public Dictionary<string, string> Hashes { get; init; } = new();
}

public sealed class GitTracking
{
    public bool IsGitRepository { get; init; }

    public string? LastIndexedCommit { get; init; }

    public Dictionary<string, string> Statuses { get; init; } = new();
}
