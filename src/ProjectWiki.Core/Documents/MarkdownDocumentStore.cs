namespace ProjectWiki.Core.Documents;

public sealed class MarkdownDocumentStore
{
    public void Write(string documentsRoot, DocumentPlan plan)
    {
        var path = Path.Combine(documentsRoot, plan.RelativePath);
        var content = File.Exists(path)
            ? File.ReadAllText(path)
            : CreateDocument(plan.Title, plan.Template);

        foreach (var (section, value) in plan.AutoSections)
        {
            content = ReplaceAutoSection(content, section, value);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateDocument(string title, DocumentTemplate template)
    {
        string[] sections = template switch
        {
            DocumentTemplate.System => ["SUMMARY", "ARCHITECTURE", "FLOW", "RELATIONS"],
            DocumentTemplate.Feature => ["SUMMARY", "FLOW", "RELATIONS"],
            DocumentTemplate.Class => ["SUMMARY", "RELATIONS"],
            _ => ["SUMMARY", "ARCHITECTURE", "RELATIONS"],
        };

        var content = $"# {title}{Environment.NewLine}";
        foreach (var section in sections)
        {
            content += $"{Environment.NewLine}<!-- AUTO:{section}:START -->{Environment.NewLine}<!-- AUTO:{section}:END -->{Environment.NewLine}";
        }

        return content + $"{Environment.NewLine}## Developer Notes{Environment.NewLine}{Environment.NewLine}";
    }

    private static string ReplaceAutoSection(string content, string section, string value)
    {
        var startMarker = $"<!-- AUTO:{section}:START -->";
        var endMarker = $"<!-- AUTO:{section}:END -->";
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        var end = content.IndexOf(endMarker, StringComparison.Ordinal);

        if (start < 0 || end < 0 || end < start + startMarker.Length)
        {
            throw new InvalidDataException($"Document is missing a valid AUTO block for '{section}'.");
        }

        var contentStart = start + startMarker.Length;
        return content[..contentStart]
            + Environment.NewLine
            + value.Trim()
            + Environment.NewLine
            + content[end..];
    }
}
