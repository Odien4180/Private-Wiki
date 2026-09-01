using ProjectWiki.Core.Scanning;
using Xunit;

namespace ProjectWiki.Core.Tests.Scanning;

public class ProjectScannerTests : IDisposable
{
    private readonly string _root;

    public ProjectScannerTests()
    {
        _root = Directory.CreateTempSubdirectory("project-wiki-scanner-tests-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Scan_ExcludesDefaultDirectoriesAndCategorizesFiles()
    {
        WriteFile("Assets/Scripts/Player.cs", "class Player {}");
        WriteFile("README.md", "# Title");
        WriteFile("obj/Debug/net10.0/generated.cs", "// should be excluded");
        WriteFile("bin/Debug/net10.0/app.dll", "binary");
        WriteFile("Library/artifact.bin", "binary");

        var scanner = new ProjectScanner();
        var files = scanner.Scan(_root, new ProjectScannerOptions { UseGit = false });

        var paths = files.Select(f => f.Path).ToList();
        Assert.Contains("Assets/Scripts/Player.cs", paths);
        Assert.Contains("README.md", paths);
        Assert.DoesNotContain(paths, p => p.StartsWith("obj/", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.StartsWith("bin/", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.StartsWith("Library/", StringComparison.Ordinal));

        var playerFile = files.Single(f => f.Path == "Assets/Scripts/Player.cs");
        Assert.Equal(FileCategory.Source, playerFile.Category);
        Assert.Equal(64, playerFile.Hash.Length);

        var readme = files.Single(f => f.Path == "README.md");
        Assert.Equal(FileCategory.Documentation, readme.Category);
    }

    [Fact]
    public void Scan_HonorsAdditionalExclusions()
    {
        WriteFile("Docs/private/secret.md", "secret");
        WriteFile("Docs/public/readme.md", "public");

        var scanner = new ProjectScanner();
        var files = scanner.Scan(_root, new ProjectScannerOptions
        {
            UseGit = false,
            AdditionalExclusions = new[] { "Docs/private/**" },
        });

        var paths = files.Select(f => f.Path).ToList();
        Assert.DoesNotContain("Docs/private/secret.md", paths);
        Assert.Contains("Docs/public/readme.md", paths);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
