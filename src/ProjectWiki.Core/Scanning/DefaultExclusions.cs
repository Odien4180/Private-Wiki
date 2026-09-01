namespace ProjectWiki.Core.Scanning;

public static class DefaultExclusions
{
    /// <summary>
    /// Glob patterns excluded from scanning by default (in addition to
    /// anything the user configures in <c>wiki.config.json</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> Patterns = new[]
    {
        ".git/**",
        "Library/**",
        "Temp/**",
        "Logs/**",
        "obj/**",
        "bin/**",
        "node_modules/**",
        "Build/**",
        "Builds/**",
    };
}
