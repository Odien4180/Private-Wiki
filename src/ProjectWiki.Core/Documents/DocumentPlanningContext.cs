using ProjectWiki.Core.Config;
using ProjectWiki.Core.Model;

namespace ProjectWiki.Core.Documents;

public sealed class DocumentPlanningContext
{
    public required string WikiTitle { get; init; }

    public required ProjectType ProjectType { get; init; }

    public required IReadOnlyList<Entity> Entities { get; init; }

    public required IReadOnlyList<Relation> Relations { get; init; }
}
