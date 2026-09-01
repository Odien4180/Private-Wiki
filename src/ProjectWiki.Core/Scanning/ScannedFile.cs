namespace ProjectWiki.Core.Scanning;

/// <summary>A single file discovered by <see cref="ProjectScanner"/>.</summary>
public sealed class ScannedFile
{
    /// <summary>Project-relative path, using forward slashes.</summary>
    public required string Path { get; init; }

    public required string Extension { get; init; }

    public required long Size { get; init; }

    public required DateTime ModifiedUtc { get; init; }

    public required string Hash { get; init; }

    /// <summary>Git porcelain status code (e.g. "M", "??"), or null when git is unavailable or the file is unchanged.</summary>
    public string? GitStatus { get; init; }

    public required FileCategory Category { get; init; }
}
