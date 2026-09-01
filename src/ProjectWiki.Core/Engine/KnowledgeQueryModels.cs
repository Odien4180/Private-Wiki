using ProjectWiki.Core.Model;
using ProjectWiki.Core.Navigation;

namespace ProjectWiki.Core.Engine;

public sealed class WikiListOptions
{
    public required string WikiRoot { get; init; }

    public string? Type { get; init; }

    public string? Source { get; init; }

    public int Limit { get; init; } = 100;

    public int Offset { get; init; }
}

public sealed class EntitySummary
{
    public required string Id { get; init; }

    public required EntityType Type { get; init; }

    public required string Title { get; init; }

    public string? Namespace { get; init; }

    public string? Assembly { get; init; }

    public List<string> Sources { get; init; } = new();

    public List<string> Members { get; init; } = new();

    public required string CodeOwnership { get; init; }
}

public sealed class WikiListResult
{
    public required int Count { get; init; }

    public required int TotalCount { get; init; }

    public required int Offset { get; init; }

    public required int Limit { get; init; }

    public List<EntitySummary> Entities { get; init; } = new();
}

public sealed class WikiContextOptions
{
    public required string WikiRoot { get; init; }

    public string? Topic { get; init; }

    public string? Source { get; init; }

    public int Depth { get; init; } = 1;

    public int Limit { get; init; } = 50;
}

public sealed class WikiContextResult
{
    public required string Query { get; init; }

    public List<EntitySummary> Entities { get; init; } = new();

    public List<Relation> IncomingRelations { get; init; } = new();

    public List<Relation> OutgoingRelations { get; init; } = new();

    public List<BacklinkReference> Backlinks { get; init; } = new();
}
