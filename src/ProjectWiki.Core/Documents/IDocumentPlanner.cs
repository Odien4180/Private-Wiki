namespace ProjectWiki.Core.Documents;

public interface IDocumentPlanner
{
    IReadOnlyList<DocumentPlan> Plan(DocumentPlanningContext context);
}
