using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ProjectWiki.Integration.Tests;

/// <summary>
/// Locates and runs the built <c>project-wiki</c> CLI executable produced
/// alongside this test assembly (the integration test project references
/// <c>ProjectWiki.Cli</c>, so building the tests also builds the CLI in
/// the same configuration).
/// </summary>
internal static class CliRunner
{
    public static (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
    {
        var dllPath = LocateCliDll();

        var psi = new ProcessStartInfo("dotnet", $"\"{dllPath}\" {string.Join(' ', args.Select(a => $"\"{a}\""))}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string LocateCliDll()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var match = Regex.Match(baseDirectory.Replace('\\', '/'), @"/bin/([^/]+)/([^/]+)/?$");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not determine build configuration from '{baseDirectory}'.");
        }

        var configuration = match.Groups[1].Value;
        var targetFramework = match.Groups[2].Value;

        var solutionRoot = FindSolutionRoot(baseDirectory);
        var dllPath = Path.Combine(solutionRoot, "src", "ProjectWiki.Cli", "bin", configuration, targetFramework, "project-wiki.dll");

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"project-wiki CLI not found at '{dllPath}'. Build the solution first.", dllPath);
        }

        return dllPath;
    }

    private static string FindSolutionRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("ProjectWiki.sln").Length > 0 || dir.GetFiles("ProjectWiki.slnx").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find ProjectWiki.sln above '{startDirectory}'.");
    }
}
