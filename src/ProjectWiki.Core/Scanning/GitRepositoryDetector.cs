using System.Diagnostics;

namespace ProjectWiki.Core.Scanning;

public sealed record GitInfo(string HeadCommit, IReadOnlyDictionary<string, string> FileStatuses);

/// <summary>
/// Thin wrapper around the <c>git</c> CLI used to enrich scanned files with
/// git status and to record the last indexed commit for change detection.
/// All facts returned here come directly from git; nothing is inferred.
/// </summary>
public static class GitRepositoryDetector
{
    public static bool IsGitRepository(string projectRoot)
    {
        return TryRunGit(projectRoot, "rev-parse --is-inside-work-tree", out var output)
            && output.Trim() == "true";
    }

    public static GitInfo? TryDetect(string projectRoot)
    {
        if (!IsGitRepository(projectRoot))
        {
            return null;
        }

        if (!TryRunGit(projectRoot, "rev-parse HEAD", out var headOutput))
        {
            return null;
        }

        var head = headOutput.Trim();

        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (TryRunGit(projectRoot, "status --porcelain", out var statusOutput))
        {
            foreach (var line in statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length < 4)
                {
                    continue;
                }

                var code = line[..2].Trim();
                var path = line[3..].Trim();
                statuses[path.Replace('\\', '/')] = code;
            }
        }

        return new GitInfo(head, statuses);
    }

    private static bool TryRunGit(string workingDirectory, string arguments, out string output)
    {
        output = string.Empty;
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
