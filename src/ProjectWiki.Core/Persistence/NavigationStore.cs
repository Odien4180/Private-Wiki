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
        var aliases = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            AddAlias(aliases, entity.Title, entity.Id);
            foreach (var alias in entity.Aliases)
            {
                AddAlias(aliases, alias, entity.Id);
            }

            foreach (var symbol in entity.Symbols)
            {
                AddAlias(aliases, symbol, entity.Id);
            }
        }

        WriteAliases(wikiRoot, new AliasIndex
        {
            Aliases = aliases
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new AliasEntry
                {
                    Alias = pair.Key,
                    Targets = pair.Value.ToList(),
                })
                .ToList(),
        });
        WriteRedirects(wikiRoot, new RedirectIndex());
        WriteBacklinks(wikiRoot, new BacklinkIndex());
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
