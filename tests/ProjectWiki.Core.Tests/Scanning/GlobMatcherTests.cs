using ProjectWiki.Core.Scanning;
using Xunit;

namespace ProjectWiki.Core.Tests.Scanning;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("Library/foo.dll", "Library/**", true)]
    [InlineData("Library/nested/foo.dll", "Library/**", true)]
    [InlineData("LIBRARY/foo.dll", "Library/**", true)]
    [InlineData("src/Library/foo.cs", "Library/**", false)]
    [InlineData("obj/Debug/net10.0/x.dll", "obj/**", true)]
    [InlineData("Assets/Scripts/Foo.cs", "Library/**", false)]
    public void IsMatch_MatchesExpected(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    [Fact]
    public void IsMatchAny_ReturnsTrueWhenAnyPatternMatches()
    {
        var patterns = DefaultExclusions.Patterns;
        Assert.True(GlobMatcher.IsMatchAny("bin/Debug/app.dll", patterns));
        Assert.False(GlobMatcher.IsMatchAny("Assets/Scripts/Foo.cs", patterns));
    }
}
