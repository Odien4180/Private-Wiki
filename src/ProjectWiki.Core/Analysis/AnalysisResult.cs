using ProjectWiki.Core.Model;

namespace ProjectWiki.Core.Analysis;

/// <summary>Result of running the static analyzers over a set of source files.</summary>
public sealed class AnalysisResult
{
    public List<Entity> Entities { get; init; } = new();

    public List<Relation> Relations { get; init; } = new();
}
