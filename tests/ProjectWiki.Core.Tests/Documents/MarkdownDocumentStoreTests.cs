using ProjectWiki.Core.Documents;
using Xunit;

namespace ProjectWiki.Core.Tests.Documents;

public class MarkdownDocumentStoreTests : IDisposable
{
    private readonly string _documentsRoot = Directory.CreateTempSubdirectory("project-wiki-documents-").FullName;

    public void Dispose()
    {
        Directory.Delete(_documentsRoot, recursive: true);
    }

    [Fact]
    public void Write_CreatesTemplateAndFillsAutoSections()
    {
        var store = new MarkdownDocumentStore();
        store.Write(_documentsRoot, CreatePlan("Generated summary"));

        var content = File.ReadAllText(Path.Combine(_documentsRoot, "architecture", "overview.md"));
        Assert.Contains("# Test Architecture", content);
        Assert.Contains("Generated summary", content);
        Assert.Contains("<!-- AUTO:ARCHITECTURE:START -->", content);
        Assert.Contains("## Developer Notes", content);
    }

    [Fact]
    public void Write_ReplacesOnlyAutoBlocksAndPreservesHumanContent()
    {
        var store = new MarkdownDocumentStore();
        store.Write(_documentsRoot, CreatePlan("Original summary"));

        var path = Path.Combine(_documentsRoot, "architecture", "overview.md");
        File.AppendAllText(path, "Keep this human-authored note.");
        store.Write(_documentsRoot, CreatePlan("Updated summary"));

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("Original summary", content);
        Assert.Contains("Updated summary", content);
        Assert.Contains("Keep this human-authored note.", content);
    }

    [Fact]
    public void Write_RejectsExistingDocumentWithoutRequestedAutoBlock()
    {
        var path = Path.Combine(_documentsRoot, "architecture", "overview.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# Existing document");

        var store = new MarkdownDocumentStore();

        Assert.Throws<InvalidDataException>(() => store.Write(_documentsRoot, CreatePlan("Summary")));
    }

    [Fact]
    public void Write_RejectsOverlappingAutoMarkers()
    {
        var path = Path.Combine(_documentsRoot, "architecture", "overview.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<!-- AUTO:SUMMARY:END --><!-- AUTO:SUMMARY:START -->");

        var store = new MarkdownDocumentStore();

        Assert.Throws<InvalidDataException>(() => store.Write(_documentsRoot, CreatePlan("Summary")));
    }

    [Fact]
    public void Write_RejectsMissingAutoEndMarker()
    {
        var path = Path.Combine(_documentsRoot, "architecture", "overview.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<!-- AUTO:SUMMARY:START -->");

        var store = new MarkdownDocumentStore();

        Assert.Throws<InvalidDataException>(() => store.Write(_documentsRoot, CreatePlan("Summary")));
    }

    private static DocumentPlan CreatePlan(string summary) => new()
    {
        RelativePath = "architecture/overview.md",
        Title = "Test Architecture",
        Template = DocumentTemplate.Architecture,
        AutoSections = new Dictionary<string, string>
        {
            ["SUMMARY"] = summary,
        },
    };
}
