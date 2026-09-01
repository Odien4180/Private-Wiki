namespace ProjectWiki.Core.Documents;

public sealed class DocumentPlan
{
    public required string RelativePath { get; init; }

    public required string Title { get; init; }

    public required DocumentTemplate Template { get; init; }

    public IReadOnlyDictionary<string, string> AutoSections { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
