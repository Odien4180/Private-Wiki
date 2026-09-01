using ProjectWiki.Core.Scanning;

namespace ProjectWiki.Core.Engine;

/// <summary>Compares snapshots by path and SHA-256 without using timestamps or git hints.</summary>
public static class ChangeDetector
{
    public static IReadOnlyList<FileChangeRecord> Detect(
        IReadOnlyDictionary<string, string> previousHashes,
        IEnumerable<ScannedFile> currentFiles)
    {
        ArgumentNullException.ThrowIfNull(previousHashes);
        ArgumentNullException.ThrowIfNull(currentFiles);

        var currentHashes = currentFiles
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToDictionary(file => file.Path, file => file.Hash, StringComparer.Ordinal);
        var changes = new List<FileChangeRecord>();
        var deleted = previousHashes.Keys.Except(currentHashes.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var added = currentHashes.Keys.Except(previousHashes.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var path in previousHashes.Keys.Intersect(currentHashes.Keys, StringComparer.Ordinal)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!string.Equals(previousHashes[path], currentHashes[path], StringComparison.Ordinal))
            {
                changes.Add(new FileChangeRecord
                {
                    Type = FileChangeType.Modified,
                    Path = path,
                    PreviousHash = previousHashes[path],
                    Hash = currentHashes[path],
                });
            }
        }

        var deletedByHash = deleted.GroupBy(path => previousHashes[path], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var addedByHash = added.GroupBy(path => currentHashes[path], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var renamedDeleted = new HashSet<string>(StringComparer.Ordinal);
        var renamedAdded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var hash in deletedByHash.Keys.Intersect(addedByHash.Keys, StringComparer.Ordinal)
                     .OrderBy(hash => hash, StringComparer.Ordinal))
        {
            var oldPaths = deletedByHash[hash];
            var newPaths = addedByHash[hash];
            if (oldPaths.Count != 1 || newPaths.Count != 1)
            {
                continue;
            }

            renamedDeleted.Add(oldPaths[0]);
            renamedAdded.Add(newPaths[0]);
            changes.Add(new FileChangeRecord
            {
                Type = FileChangeType.Renamed,
                Path = newPaths[0],
                PreviousPath = oldPaths[0],
                PreviousHash = hash,
                Hash = hash,
            });
        }

        changes.AddRange(deleted.Where(path => !renamedDeleted.Contains(path)).Select(path => new FileChangeRecord
        {
            Type = FileChangeType.Deleted,
            Path = path,
            PreviousHash = previousHashes[path],
        }));
        changes.AddRange(added.Where(path => !renamedAdded.Contains(path)).Select(path => new FileChangeRecord
        {
            Type = FileChangeType.Added,
            Path = path,
            Hash = currentHashes[path],
        }));

        return changes
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ThenBy(change => change.PreviousPath, StringComparer.Ordinal)
            .ThenBy(change => change.Type)
            .ToList();
    }
}
