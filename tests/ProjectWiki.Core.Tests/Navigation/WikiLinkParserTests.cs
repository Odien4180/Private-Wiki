using ProjectWiki.Core.Navigation;

namespace ProjectWiki.Core.Tests.Navigation;

public class WikiLinkParserTests
{
    [Fact]
    public void Parse_ExtractsTargetsAndDisplayText()
    {
        var links = WikiLinkParser.Parse("architecture/overview.md", """
            [[combat-system]]
            [[character-system|Character System]]
            """);

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal("combat-system", link.Target);
                Assert.Null(link.DisplayText);
                Assert.Equal(1, link.Line);
                Assert.Equal(1, link.Column);
            },
            link =>
            {
                Assert.Equal("character-system", link.Target);
                Assert.Equal("Character System", link.DisplayText);
                Assert.Equal(2, link.Line);
                Assert.Equal(1, link.Column);
            });
    }

    [Fact]
    public void Parse_IgnoresEscapedAndCodeLinks()
    {
        var links = WikiLinkParser.Parse("architecture/overview.md", """
            \[[escaped]]
            `[[inline-code]]`
            ```text
            [[fenced-code]]
            ```
            [[included]]
            """);

        var link = Assert.Single(links);
        Assert.Equal("included", link.Target);
        Assert.Equal(6, link.Line);
    }

    [Fact]
    public void Parse_RecordsMalformedLinkLocation()
    {
        var link = Assert.Single(WikiLinkParser.Parse("architecture/overview.md", "Before [[unfinished"));

        Assert.True(link.IsMalformed);
        Assert.Equal(8, link.Column);
        Assert.Equal(1, link.Line);
    }
}
