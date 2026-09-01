namespace ProjectWiki.Core.Navigation;

public static class WikiLinkParser
{
    public static IReadOnlyList<WikiLink> Parse(string documentPath, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(markdown);

        var links = new List<WikiLink>();
        using var reader = new StringReader(markdown);
        var lineNumber = 0;
        char? fence = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (IsFence(trimmed, out var fenceCharacter))
            {
                if (fence is null)
                {
                    fence = fenceCharacter;
                }
                else if (fence == fenceCharacter)
                {
                    fence = null;
                }

                continue;
            }

            if (fence is null)
            {
                ParseLine(documentPath, line, lineNumber, links);
            }
        }

        return links;
    }

    private static void ParseLine(string documentPath, string line, int lineNumber, List<WikiLink> links)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '`')
            {
                index = SkipInlineCode(line, index);
                continue;
            }

            if (line[index] != '['
                || index + 1 >= line.Length
                || line[index + 1] != '['
                || IsEscaped(line, index))
            {
                continue;
            }

            var close = line.IndexOf("]]", index + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                links.Add(new WikiLink
                {
                    DocumentPath = documentPath,
                    Line = lineNumber,
                    Column = index + 1,
                    Target = string.Empty,
                    IsMalformed = true,
                });
                break;
            }

            var contents = line[(index + 2)..close];
            var separator = contents.IndexOf('|');
            var target = (separator < 0 ? contents : contents[..separator]).Trim();
            var displayText = separator < 0 ? null : contents[(separator + 1)..].Trim();
            links.Add(new WikiLink
            {
                DocumentPath = documentPath,
                Line = lineNumber,
                Column = index + 1,
                Target = target,
                DisplayText = displayText,
                IsMalformed = string.IsNullOrWhiteSpace(target),
            });
            index = close + 1;
        }
    }

    private static bool IsFence(string trimmedLine, out char character)
    {
        character = '\0';
        if (trimmedLine.Length < 3 || (trimmedLine[0] != '`' && trimmedLine[0] != '~'))
        {
            return false;
        }

        character = trimmedLine[0];
        return trimmedLine[1] == character && trimmedLine[2] == character;
    }

    private static int SkipInlineCode(string line, int openingIndex)
    {
        var delimiterLength = 1;
        while (openingIndex + delimiterLength < line.Length
            && line[openingIndex + delimiterLength] == '`')
        {
            delimiterLength++;
        }

        var delimiter = new string('`', delimiterLength);
        var closingIndex = line.IndexOf(delimiter, openingIndex + delimiterLength, StringComparison.Ordinal);
        return closingIndex < 0 ? line.Length : closingIndex + delimiterLength - 1;
    }

    private static bool IsEscaped(string line, int index)
    {
        var backslashes = 0;
        for (var cursor = index - 1; cursor >= 0 && line[cursor] == '\\'; cursor--)
        {
            backslashes++;
        }

        return backslashes % 2 != 0;
    }
}
