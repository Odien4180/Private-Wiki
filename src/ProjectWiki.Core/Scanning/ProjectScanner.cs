namespace ProjectWiki.Core.Scanning;

public sealed class ProjectScannerOptions
{
    public IReadOnlyList<string> AdditionalExclusions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> IncludePatterns { get; init; } = Array.Empty<string>();

    public bool UseGit { get; init; } = true;
}

/// <summary>
/// Deterministically walks a project root and collects file-level facts
/// (path, size, mtime, hash, git status, category). Performs no semantic
/// analysis.
/// </summary>
public sealed class ProjectScanner
{
    public IReadOnlyList<ScannedFile> Scan(string projectRoot, ProjectScannerOptions? options = null)
    {
        options ??= new ProjectScannerOptions();
        var fullRoot = System.IO.Path.GetFullPath(projectRoot);

        var exclusions = DefaultExclusions.Patterns.Concat(options.AdditionalExclusions).ToList();

        var gitInfo = options.UseGit ? GitRepositoryDetector.TryDetect(fullRoot) : null;

        var results = new List<ScannedFile>();
        foreach (var filePath in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = System.IO.Path.GetRelativePath(fullRoot, filePath).Replace('\\', '/');

            if (GlobMatcher.IsMatchAny(relativePath, exclusions)
                || (options.IncludePatterns.Count > 0 && !GlobMatcher.IsMatchAny(relativePath, options.IncludePatterns)))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var extension = System.IO.Path.GetExtension(filePath);

            string? gitStatus = null;
            gitInfo?.FileStatuses.TryGetValue(relativePath, out gitStatus);

            results.Add(new ScannedFile
            {
                Path = relativePath,
                Extension = extension,
                Size = info.Length,
                ModifiedUtc = info.LastWriteTimeUtc,
                Hash = FileHasher.ComputeSha256(filePath),
                GitStatus = gitStatus,
                Category = FileCategorizer.Categorize(extension),
            });
        }

        return results.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
    }
}
