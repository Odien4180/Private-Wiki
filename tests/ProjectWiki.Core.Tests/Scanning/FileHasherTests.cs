using ProjectWiki.Core.Scanning;
using Xunit;

namespace ProjectWiki.Core.Tests.Scanning;

public class FileHasherTests : IDisposable
{
    private readonly string _root;

    public FileHasherTests()
    {
        _root = Directory.CreateTempSubdirectory("project-wiki-hasher-tests-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ComputeSha256_IsStableForSameContent()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "hello world");

        var first = FileHasher.ComputeSha256(path);
        var second = FileHasher.ComputeSha256(path);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ComputeSha256_ChangesWhenContentChanges()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "hello world");
        var before = FileHasher.ComputeSha256(path);

        File.WriteAllText(path, "hello world!");
        var after = FileHasher.ComputeSha256(path);

        Assert.NotEqual(before, after);
    }
}
