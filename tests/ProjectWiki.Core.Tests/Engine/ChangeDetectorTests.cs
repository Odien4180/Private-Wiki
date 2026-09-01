using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Scanning;
using Xunit;

namespace ProjectWiki.Core.Tests.Engine;

public class ChangeDetectorTests
{
    [Fact]
    public void Detect_ClassifiesHashChangesAndUnambiguousRenamesDeterministically()
    {
        var previous = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["changed.cs"] = "old",
            ["deleted.cs"] = "deleted",
            ["old-name.cs"] = "same",
        };
        var current = new[]
        {
            File("added.cs", "added"),
            File("changed.cs", "new"),
            File("new-name.cs", "same"),
        };

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Collection(changes,
            change =>
            {
                Assert.Equal(FileChangeType.Added, change.Type);
                Assert.Equal("added.cs", change.Path);
                Assert.Equal("added", change.Hash);
            },
            change =>
            {
                Assert.Equal(FileChangeType.Modified, change.Type);
                Assert.Equal("changed.cs", change.Path);
                Assert.Equal("old", change.PreviousHash);
                Assert.Equal("new", change.Hash);
            },
            change =>
            {
                Assert.Equal(FileChangeType.Deleted, change.Type);
                Assert.Equal("deleted.cs", change.Path);
                Assert.Equal("deleted", change.PreviousHash);
            },
            change =>
            {
                Assert.Equal(FileChangeType.Renamed, change.Type);
                Assert.Equal("new-name.cs", change.Path);
                Assert.Equal("old-name.cs", change.PreviousPath);
                Assert.Equal("same", change.Hash);
            });
    }

    [Fact]
    public void Detect_DoesNotGuessRenamesForDuplicateHashes()
    {
        var previous = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["one.cs"] = "shared",
            ["two.cs"] = "shared",
        };
        var current = new[]
        {
            File("three.cs", "shared"),
            File("four.cs", "shared"),
        };

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Equal(4, changes.Count);
        Assert.DoesNotContain(changes, change => change.Type == FileChangeType.Renamed);
        Assert.Equal(2, changes.Count(change => change.Type == FileChangeType.Added));
        Assert.Equal(2, changes.Count(change => change.Type == FileChangeType.Deleted));
    }

    private static ScannedFile File(string path, string hash) => new()
    {
        Path = path,
        Extension = ".cs",
        Size = 1,
        ModifiedUtc = DateTime.UnixEpoch,
        Hash = hash,
        Category = FileCategory.Source,
    };
}
