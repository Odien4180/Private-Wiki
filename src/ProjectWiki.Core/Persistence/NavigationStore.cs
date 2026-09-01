using ProjectWiki.Core.Model;
using ProjectWiki.Core.Navigation;

namespace ProjectWiki.Core.Persistence;

public sealed class NavigationStore
{
    public NavigationData Load(string wikiRoot)
    {
        var knowledgeRoot = Path.Combine(Path.GetFullPath(wikiRoot), "knowledge");
        var backlinksPath = Path.Combine(knowledgeRoot, "backlinks.json");
        return new NavigationData
        {
            Entities = AtomicFile.ReadJson<EntityCatalog>(Path.Combine(knowledgeRoot, "entities.json")),
            Aliases = ReadOrDefault<AliasIndex>(Path.Combine(knowledgeRoot, "aliases.json")),
            Redirects = ReadOrDefault<RedirectIndex>(Path.Combine(knowledgeRoot, "redirects.json")),
            Backlinks = File.Exists(backlinksPath) ? AtomicFile.ReadJson<BacklinkIndex>(backlinksPath) : new BacklinkIndex(),
            HasPersistedBacklinks = File.Exists(backlinksPath),
        };
    }

    public void Initialize(string wikiRoot, IEnumerable<Entity> entities)
    {
        WriteAliases(wikiRoot, CreateGeneratedAliases(entities));
        WriteRedirects(wikiRoot, new RedirectIndex());
        WriteBacklinks(wikiRoot, new BacklinkIndex());
    }

    /// <summary>
    /// Rebuilds analyzer-owned aliases while retaining existing aliases that
    /// still target a current entity. Redirects are deliberately not touched.
    /// </summary>
    public void RefreshAliases(string wikiRoot, IEnumerable<Entity> entities)
    {
        var entityList = entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToList();
        var entityIds = entityList.ToDictionary(entity => entity.Id, StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var existing = ReadOrDefault<AliasIndex>(
            Path.Combine(Path.GetFullPath(wikiRoot), "knowledge", "aliases.json"));

        foreach (var entry in existing.Aliases.OrderBy(entry => entry.Alias, StringComparer.Ordinal))
        {
            foreach (var target in entry.Targets.OrderBy(target => target, StringComparer.Ordinal))
            {
                if (entityIds.TryGetValue(target, out var entity))
                {
                    AddAlias(aliases, entry.Alias, entity.Id);
                }
            }
        }

        AddEntityAliases(aliases, entityList);
        WriteAliases(wikiRoot, ToAliasIndex(aliases));
    }

    public void WriteAliases(string wikiRoot, AliasIndex aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        AtomicFile.WriteJson(Path.Combine(Path.GetFullPath(wikiRoot), "knowledge", "aliases.json"), new AliasIndex
        {
            Aliases = aliases.Aliases
                .OrderBy(alias => alias.Alias, StringComparer.Ordinal)
                .ThenBy(alias => string.Join("\u001F", alias.Targets), StringComparer.Ordinal)
                .Select(alias => new AliasEntry
                {
                    Alias = alias.Alias,
                    Targets = alias.Targets.OrderBy(target => target, StringComparer.Ordinal).ToList(),
                })
                .ToList(),
        });
    }

    public void WriteRedirects(string wikiRoot, RedirectIndex redirects)
    {
        ArgumentNullException.ThrowIfNull(redirects);
        AtomicFile.WriteJson(Path.Combine(Path.GetFullPath(wikiRoot), "knowledge", "redirects.json"), new RedirectIndex
        {
            Redirects = redirects.Redirects
                .OrderBy(redirect => redirect.From, StringComparer.Ordinal)
                .ThenBy(redirect => redirect.To, StringComparer.Ordinal)
                .ToList(),
        });
    }

    public void WriteBacklinks(string wikiRoot, BacklinkIndex backlinks)
    {
        ArgumentNullException.ThrowIfNull(backlinks);
        AtomicFile.WriteJson(Path.Combine(Path.GetFullPath(wikiRoot), "knowledge", "backlinks.json"), backlinks);
    }

    private static T ReadOrDefault<T>(string path)
        where T : new() => File.Exists(path) ? AtomicFile.ReadJson<T>(path) : new T();

    private static AliasIndex CreateGeneratedAliases(IEnumerable<Entity> entities)
    {
        var aliases = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        AddEntityAliases(aliases, entities.OrderBy(entity => entity.Id, StringComparer.Ordinal));
        return ToAliasIndex(aliases);
    }

    private static void AddEntityAliases(IDictionary<string, SortedSet<string>> aliases, IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            AddAlias(aliases, entity.Title, entity.Id);
            foreach (var alias in entity.Aliases.OrderBy(alias => alias, StringComparer.Ordinal))
            {
                AddAlias(aliases, alias, entity.Id);
            }

            foreach (var symbol in entity.Symbols.OrderBy(symbol => symbol, StringComparer.Ordinal))
            {
                AddAlias(aliases, symbol, entity.Id);
            }
        }
    }

    private static AliasIndex ToAliasIndex(IReadOnlyDictionary<string, SortedSet<string>> aliases) => new()
    {
        Aliases = aliases
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AliasEntry
            {
                Alias = pair.Key,
                Targets = pair.Value.ToList(),
            })
            .ToList(),
    };

    private static void AddAlias(IDictionary<string, SortedSet<string>> aliases, string? alias, string entityId)
    {
        var normalized = alias?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return;
        }

        if (!aliases.TryGetValue(normalized, out var targets))
        {
            targets = new SortedSet<string>(StringComparer.Ordinal);
            aliases[normalized] = targets;
        }

        targets.Add(entityId);
    }
}

public sealed class NavigationData
{
    public required EntityCatalog Entities { get; init; }

    public required AliasIndex Aliases { get; init; }

    public required RedirectIndex Redirects { get; init; }

    public required BacklinkIndex Backlinks { get; init; }

    public required bool HasPersistedBacklinks { get; init; }
}
