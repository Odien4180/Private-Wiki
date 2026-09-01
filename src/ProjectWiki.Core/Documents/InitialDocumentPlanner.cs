namespace ProjectWiki.Core.Documents;

/// <summary>
/// Produces the deterministic initial architecture document. Semantic system,
/// feature, and class planning remains the responsibility of an agent.
/// </summary>
public sealed class InitialDocumentPlanner : IDocumentPlanner
{
    public IReadOnlyList<DocumentPlan> Plan(DocumentPlanningContext context)
    {
        return
        [
            new DocumentPlan
            {
                RelativePath = "architecture/overview.md",
                Title = $"{context.WikiTitle} Architecture",
                Template = DocumentTemplate.Architecture,
                AutoSections = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["SUMMARY"] = $"This wiki indexes a {GetProjectTypeName(context.ProjectType)} project with {context.Entities.Count} extracted entities and {context.Relations.Count} extracted relations.",
                    ["ARCHITECTURE"] = CreateArchitectureSummary(context),
                    ["RELATIONS"] = $"The knowledge graph currently contains {context.Relations.Count} structural relations. Navigation between documents is added in Milestone 3.",
                },
            },
        ];
    }

    private static string CreateArchitectureSummary(DocumentPlanningContext context)
    {
        if (context.Entities.Count == 0)
        {
            return "No source entities were extracted during the initial analysis.";
        }

        var groups = context.Entities
            .GroupBy(entity => entity.Type)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => $"- {group.Key}: {group.Count()}");

        return string.Join(Environment.NewLine, groups);
    }

    private static string GetProjectTypeName(ProjectWiki.Core.Config.ProjectType projectType) => projectType switch
    {
        ProjectWiki.Core.Config.ProjectType.DotNet => ".NET",
        ProjectWiki.Core.Config.ProjectType.Unity => "Unity",
        _ => "generic",
    };
}
