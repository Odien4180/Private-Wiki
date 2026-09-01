using System.Text;

namespace ProjectWiki.Core.Documents;

public sealed class MarkdownDocumentStore
{
    public void Write(string documentsRoot, DocumentPlan plan)
    {
        var path = Path.Combine(documentsRoot, plan.RelativePath);
        var exists = File.Exists(path);
        var content = exists ? File.ReadAllText(path) : CreateDocument(plan.Title, plan.Template);

        if (exists)
        {
            ValidateTemplate(content, plan.Template);
        }

        foreach (var (section, value) in plan.AutoSections)
        {
            content = ReplaceAutoSection(content, section, value);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateDocument(string title, DocumentTemplate template)
    {
        var sections = GetSections(template);
        var content = new StringBuilder($"# {title}{Environment.NewLine}");
        foreach (var section in sections)
        {
            content.Append($"{Environment.NewLine}<!-- AUTO:{section}:START -->{Environment.NewLine}<!-- AUTO:{section}:END -->{Environment.NewLine}");
        }

        content.Append($"{Environment.NewLine}## Developer Notes{Environment.NewLine}{Environment.NewLine}");
        return content.ToString();
    }

    private static string[] GetSections(DocumentTemplate template) => template switch
    {
        DocumentTemplate.System => ["SUMMARY", "ARCHITECTURE", "FLOW", "RELATIONS"],
        DocumentTemplate.Feature => ["SUMMARY", "FLOW", "RELATIONS"],
        DocumentTemplate.Class => ["SUMMARY", "RELATIONS"],
        _ => ["SUMMARY", "ARCHITECTURE", "RELATIONS"],
    };

    private static void ValidateTemplate(string content, DocumentTemplate template)
    {
        foreach (var section in GetSections(template))
        {
            if (!content.Contains($"<!-- AUTO:{section}:START -->", StringComparison.Ordinal)
                || !content.Contains($"<!-- AUTO:{section}:END -->", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Document does not match the '{template}' template.");
            }
        }
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
