using System.Text.Json;

namespace ProjectWiki.Core.Persistence;

/// <summary>Lower-cases enum member names with no word separation (e.g. <c>DotNet</c> -&gt; <c>dotnet</c>).</summary>
public sealed class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public static readonly LowerCaseNamingPolicy Instance = new();

    public override string ConvertName(string name) => name.ToLowerInvariant();
}
