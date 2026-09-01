using System.Text.Json.Serialization;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Scanning;

[JsonConverter(typeof(LowerCaseEnumConverter<FileCategory>))]
public enum FileCategory
{
    Source,
    Config,
    Asset,
    Documentation,
    Other,
}

public static class FileCategorizer
{
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".go", ".rs", ".java", ".kt", ".cpp", ".c", ".h", ".hpp",
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yml", ".yaml", ".toml", ".ini", ".config", ".asmdef", ".csproj", ".sln",
    };

    private static readonly HashSet<string> DocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".mdx", ".txt", ".rst",
    };

    private static readonly HashSet<string> AssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".unity", ".prefab", ".asset", ".meta", ".png", ".jpg", ".jpeg", ".fbx", ".wav", ".mp3", ".anim",
    };

    public static FileCategory Categorize(string extension)
    {
        if (SourceExtensions.Contains(extension))
        {
            return FileCategory.Source;
        }

        if (ConfigExtensions.Contains(extension))
        {
            return FileCategory.Config;
        }

        if (DocumentationExtensions.Contains(extension))
        {
            return FileCategory.Documentation;
        }

        if (AssetExtensions.Contains(extension))
        {
            return FileCategory.Asset;
        }

        return FileCategory.Other;
    }
}
