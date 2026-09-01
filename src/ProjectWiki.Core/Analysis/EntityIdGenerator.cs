using System.Text.RegularExpressions;

namespace ProjectWiki.Core.Analysis;

/// <summary>
/// Converts symbol names to stable, kebab-case entity identifiers
/// (e.g. <c>CharacterController</c> -&gt; <c>character-controller</c>).
/// </summary>
public static class EntityIdGenerator
{
    private static readonly Regex LowerToUpper = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex AcronymBoundary = new("([A-Z]+)([A-Z][a-z])", RegexOptions.Compiled);
    private static readonly Regex NonIdChars = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static string FromSymbolName(string name)
    {
        var withBoundaries = AcronymBoundary.Replace(LowerToUpper.Replace(name, "$1-$2"), "$1-$2");
        var lowered = withBoundaries.ToLowerInvariant();
        var cleaned = NonIdChars.Replace(lowered, "-").Trim('-');
        return cleaned;
    }

    /// <summary>
    /// Builds a unique id for <paramref name="name"/>, falling back to a
    /// namespace-qualified id when the plain name collides with a
    /// different symbol that already claimed it.
    /// </summary>
    public static string MakeUnique(string name, string? containingNamespace, HashSet<string> usedIds)
    {
        var candidate = FromSymbolName(name);
        if (usedIds.Add(candidate))
        {
            return candidate;
        }

        var qualified = string.IsNullOrEmpty(containingNamespace)
            ? candidate
            : $"{FromSymbolName(containingNamespace)}-{candidate}";

        if (usedIds.Add(qualified))
        {
            return qualified;
        }

        var suffix = 2;
        string deduped;
        do
        {
            deduped = $"{qualified}-{suffix++}";
        }
        while (!usedIds.Add(deduped));

        return deduped;
    }
}
