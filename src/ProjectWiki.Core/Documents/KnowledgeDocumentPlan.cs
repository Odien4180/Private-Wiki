namespace ProjectWiki.Core.Documents;

public sealed class KnowledgeDocumentPlan
{
    public List<DocumentPlanCandidate> Architecture { get; init; } = new();

    public List<DocumentPlanCandidate> Systems { get; init; } = new();

    public List<DocumentPlanCandidate> Features { get; init; } = new();

    public List<DocumentPlanCandidate> Classes { get; init; } = new();

    public List<DocumentPlanCandidate> Scenes { get; init; } = new();

    public List<DocumentPlanCandidate> Data { get; init; } = new();

    public List<DocumentPlanCandidate> Packages { get; init; } = new();
}

public sealed class DocumentPlanCandidate
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Reason { get; init; }

    public List<string> EntityIds { get; init; } = new();

    public List<string> Sources { get; init; } = new();

    public List<string> Evidence { get; init; } = new();
}
