using ProjectWiki.Core.Config;

namespace ProjectWiki.Core.Engine;

public static class ProjectTypeDetector
{
    public static ProjectType Detect(string projectRoot)
    {
        var hasUnityMarkers =
            Directory.Exists(Path.Combine(projectRoot, "Assets")) &&
            Directory.Exists(Path.Combine(projectRoot, "ProjectSettings"));

        if (hasUnityMarkers)
        {
            return ProjectType.Unity;
        }

        var hasDotNetMarkers = Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.AllDirectories).Any()
            || Directory.EnumerateFiles(projectRoot, "*.sln", SearchOption.AllDirectories).Any();

        return hasDotNetMarkers ? ProjectType.DotNet : ProjectType.Generic;
    }
}
