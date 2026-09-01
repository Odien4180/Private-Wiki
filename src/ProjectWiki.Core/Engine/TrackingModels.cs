using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Engine;

public sealed class TrackedFileRecord
{
    public required string Path { get; init; }

    public required string Extension { get; init; }

    public required long Size { get; init; }

    public required DateTime ModifiedUtc { get; init; }

    public required string Category { get; init; }
}

public sealed class FilesTracking
{
    public List<TrackedFileRecord> Files { get; init; } = new();
}

public sealed class HashesTracking
{
    public Dictionary<string, string> Hashes { get; set; } = new();
}

public sealed class RelationCatalog
{
    public List<ProjectWiki.Core.Model.Relation> Relations { get; init; } = new();
}

public sealed class GitTracking
{
    public bool IsGitRepository { get; init; }

    public string? LastIndexedCommit { get; init; }

    public Dictionary<string, string> Statuses { get; init; } = new();
}

[JsonConverter(typeof(LowerCaseEnumConverter<FileChangeType>))]
public enum FileChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed,
}

/// <summary>
/// A source-tree change determined exclusively by comparing persisted and
/// current SHA-256 hashes. <see cref="PreviousPath"/> is populated for a
/// rename; hashes are retained so the record is independently auditable.
/// </summary>
public sealed class FileChangeRecord
{
    public required FileChangeType Type { get; init; }

    public required string Path { get; init; }

    public string? PreviousPath { get; init; }

    public string? PreviousHash { get; init; }

    public string? Hash { get; init; }
}

public sealed class RelationImpact
{
    public List<string> DirectEntityIds { get; init; } = new();

    public List<string> RelatedEntityIds { get; init; } = new();

    public List<string> AffectedEntityIds { get; init; } = new();

    public int AffectedRelationCount { get; init; }
}

/// <summary>A persisted, typed summary of one incremental indexing pass.</summary>
public sealed class UpdateRecord
{
    public required DateTime IndexedUtc { get; init; }

    public required bool IsRebuild { get; init; }

    public List<FileChangeRecord> Changes { get; init; } = new();

    public required RelationImpact Impact { get; init; }
}

public sealed class UpdatesTracking
{
    public List<UpdateRecord> Updates { get; init; } = new();
}

public sealed class WikiUpdateOptions
{
    public required string WikiRoot { get; init; }
}

public sealed class WikiRebuildOptions
{
    public required string WikiRoot { get; init; }
}

public sealed class WikiUpdateResult
{
    public required string WikiRoot { get; init; }

    public required bool IsRebuild { get; init; }

    public required int ScannedFileCount { get; init; }

    public required int EntityCount { get; init; }

    public required int RelationCount { get; init; }

    public required bool IsGitRepository { get; init; }

    public List<FileChangeRecord> Changes { get; init; } = new();

    public required RelationImpact Impact { get; init; }
}

public sealed class WikiInspectOptions
{
    public required string WikiRoot { get; init; }

    public required string Entity { get; init; }

    public int Depth { get; init; } = 1;
}

public sealed class WikiInspectResult
{
    public required string Query { get; init; }

    public required bool IsFound { get; init; }

    public required bool IsAmbiguous { get; init; }

    public string? EntityId { get; init; }

    public ProjectWiki.Core.Model.Entity? Entity { get; init; }

    public List<ProjectWiki.Core.Model.Relation> Relations { get; init; } = new();

    public List<ProjectWiki.Core.Navigation.BacklinkReference> Backlinks { get; init; } = new();
}
