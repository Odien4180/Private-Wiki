using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ProjectWiki.Core.Scanning;

/// <summary>
/// Minimal glob matcher supporting <c>*</c>, <c>?</c> and <c>**</c> against
/// forward-slash-normalized, project-relative paths. This is intentionally
/// small: it only needs to support the exclusion patterns used by the
/// project scanner (e.g. <c>Library/**</c>, <c>obj/**</c>).
/// </summary>
public static class GlobMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    public static bool IsMatch(string relativePath, string pattern)
    {
        var normalizedPath = Normalize(relativePath);
        var regex = GetOrBuildRegex(pattern);
        return regex.IsMatch(normalizedPath);
    }

    public static bool IsMatchAny(string relativePath, IEnumerable<string> patterns)
    {
        var normalizedPath = Normalize(relativePath);
        foreach (var pattern in patterns)
        {
            if (GetOrBuildRegex(pattern).IsMatch(normalizedPath))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static Regex GetOrBuildRegex(string pattern) =>
        RegexCache.GetOrAdd(Normalize(pattern), BuildRegex);

    private static Regex BuildRegex(string pattern)
    {
        var sb = new System.Text.StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                sb.Append(".*");
                i++;
                if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                {
                    i++;
                }
            }
            else if (c == '*')
            {
                sb.Append("[^/]*");
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
