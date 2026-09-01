using System.Text.Json;
using System.Net;
using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Navigation;
using ProjectWiki.Core.Persistence;
using ProjectWiki.Core.Scanning;
using ProjectWiki.Core.Site;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0];
var commandArguments = args.Skip(1).ToArray();
var options = ParseOptions(commandArguments);

switch (command)
{
    case "init":
        return RunInit(options);
    case "scope":
        return RunScope(options);
    case "update":
        return RunUpdate(options, isRebuild: false);
    case "rebuild":
        return RunUpdate(options, isRebuild: true);
    case "inspect":
        return RunInspect(GetInspectEntity(commandArguments), options);
    case "list":
        return RunList(options);
    case "context":
        return RunContext(options);
    case "validate":
        return RunValidate(options);
    case "build":
        return RunBuild(options);
    case "serve":
        return RunServe(options);
    case "--help":
    case "-h":
    case "help":
        PrintUsage();
        return 0;
    default:
        Console.Error.WriteLine($"Unknown command: '{command}'.");
        PrintUsage();
        return 1;
}

static int RunInit(Dictionary<string, string> options)
{
    if (!options.TryGetValue("project", out var projectRoot) || string.IsNullOrWhiteSpace(projectRoot))
    {
        Console.Error.WriteLine("Missing required option: --project <path>");
        return 1;
    }

    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().Init(new WikiInitOptions
        {
            ProjectRoot = projectRoot,
            WikiRoot = wikiRoot,
            Title = options.GetValueOrDefault("title"),
            Language = options.GetValueOrDefault("language", "ko"),
            AdditionalExclusions = GetOptionValues(options, "exclude"),
            IncludePatterns = GetOptionValues(options, "include"),
        });

        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"init failed: {ex.Message}");
        return 1;
    }
}

static int RunScope(Dictionary<string, string> options)
{
    if (!options.TryGetValue("project", out var projectRoot) || string.IsNullOrWhiteSpace(projectRoot))
    {
        Console.Error.WriteLine("Missing required option: --project <path>");
        return 1;
    }

    try
    {
        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var projectType = ProjectTypeDetector.Detect(fullProjectRoot);
        var report = new ProjectScopeAnalyzer().Analyze(
            fullProjectRoot,
            projectType,
            GetOptionValues(options, "exclude"),
            GetOptionValues(options, "include"));
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or ArgumentException)
    {
        Console.Error.WriteLine($"scope failed: {ex.Message}");
        return 1;
    }
}

static int RunUpdate(Dictionary<string, string> options, bool isRebuild)
{
    var operationName = isRebuild ? "rebuild" : "update";
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var engine = new WikiEngine();
        var result = isRebuild
            ? engine.Rebuild(new WikiRebuildOptions { WikiRoot = wikiRoot })
            : engine.Update(new WikiUpdateOptions { WikiRoot = wikiRoot });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"{operationName} failed: {ex.Message}");
        return 1;
    }
}

static int RunInspect(string? entity, Dictionary<string, string> options)
{
    if (string.IsNullOrWhiteSpace(entity))
    {
        Console.Error.WriteLine("Missing required entity: inspect <entity> --wiki <path>");
        return 1;
    }

    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().Inspect(new WikiInspectOptions
        {
            WikiRoot = wikiRoot,
            Entity = entity,
            Depth = TryGetInt(options, "depth", defaultValue: 1, min: 1),
        });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return result.IsFound ? 0 : 1;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"inspect failed: {ex.Message}");
        return 1;
    }
}

static int RunList(Dictionary<string, string> options)
{
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().List(new WikiListOptions
        {
            WikiRoot = wikiRoot,
            Type = options.GetValueOrDefault("type"),
            Source = options.GetValueOrDefault("source"),
        });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"list failed: {ex.Message}");
        return 1;
    }
}

static int RunContext(Dictionary<string, string> options)
{
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().Context(new WikiContextOptions
        {
            WikiRoot = wikiRoot,
            Topic = options.GetValueOrDefault("topic"),
            Source = options.GetValueOrDefault("source"),
            Depth = TryGetInt(options, "depth", defaultValue: 1, min: 1),
        });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"context failed: {ex.Message}");
        return 1;
    }
}

static int RunBuild(Dictionary<string, string> options)
{
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().BuildSite(new WikiBuildOptions { WikiRoot = wikiRoot });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"build failed: {ex.Message}");
        return 1;
    }
}

static int RunValidate(Dictionary<string, string> options)
{
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    try
    {
        var result = new WikiEngine().ValidateNavigation(new WikiNavigationOptions
        {
            WikiRoot = wikiRoot,
            RequireDocuments = options.ContainsKey("require-documents"),
            MinCoverage = TryGetDouble(options, "min-coverage", defaultValue: 0),
        });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return result.IsValid ? 0 : 1;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
    {
        Console.Error.WriteLine($"validate failed: {ex.Message}");
        return 1;
    }
}

static int RunServe(Dictionary<string, string> options)
{
    if (!options.TryGetValue("wiki", out var wikiRoot) || string.IsNullOrWhiteSpace(wikiRoot))
    {
        Console.Error.WriteLine("Missing required option: --wiki <path>");
        return 1;
    }

    if (!TryGetPort(options, out var port))
    {
        Console.Error.WriteLine("Invalid option: --port must be an integer between 1 and 65535.");
        return 1;
    }

    try
    {
        var result = new WikiEngine().Serve(new WikiServeOptions { WikiRoot = wikiRoot, Port = port });
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
        return 0;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or HttpListenerException)
    {
        Console.Error.WriteLine($"serve failed: {ex.Message}");
        return 1;
    }
}

static string? GetInspectEntity(string[] arguments)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            index++;
            continue;
        }

        return arguments[index];
    }

    return null;
}

static Dictionary<string, string> ParseOptions(string[] rest)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < rest.Length; i++)
    {
        var token = rest[i];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var name = token[2..];
        var value = i + 1 < rest.Length && !rest[i + 1].StartsWith("--", StringComparison.Ordinal)
            ? rest[++i]
            : "true";

        result[name] = result.TryGetValue(name, out var prior)
            ? prior + "\n" + value
            : value;
    }

    return result;
}

static IReadOnlyList<string> GetOptionValues(Dictionary<string, string> options, string name)
{
    if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
    {
        return Array.Empty<string>();
    }

    return value.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static int TryGetInt(Dictionary<string, string> options, string name, int defaultValue, int min)
{
    if (!options.TryGetValue(name, out var value))
    {
        return defaultValue;
    }

    if (!int.TryParse(value, out var parsed) || parsed < min)
    {
        throw new ArgumentException($"--{name} must be an integer greater than or equal to {min}.");
    }

    return parsed;
}

static double TryGetDouble(Dictionary<string, string> options, string name, double defaultValue)
{
    if (!options.TryGetValue(name, out var value))
    {
        return defaultValue;
    }

    if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
    {
        throw new ArgumentException($"--{name} must be a number.");
    }

    return parsed;
}

static bool TryGetPort(Dictionary<string, string> options, out int port)
{
    port = 8080;
    return !options.TryGetValue("port", out var value)
        || (int.TryParse(value, out port) && port is >= 1 and <= 65535);
}

static void PrintUsage()
{
    Console.WriteLine("""
        project-wiki - reusable project wiki engine (Milestone 6: static site)

        Usage:
          project-wiki scope --project <path> [--include <glob>] [--exclude <glob>]
          project-wiki init --project <path> --wiki <path> [--title <title>] [--language <lang>] [--include <glob>] [--exclude <glob>]
          project-wiki update --wiki <path>
          project-wiki rebuild --wiki <path>
          project-wiki list --wiki <path> [--type <type>] [--source <glob>]
          project-wiki inspect <entity> --wiki <path> [--depth <n>]
          project-wiki context --wiki <path> [--topic <text>] [--source <glob>] [--depth <n>]
          project-wiki validate --wiki <path> [--require-documents] [--min-coverage <0..1>]
          project-wiki build --wiki <path>
          project-wiki serve --wiki <path> [--port <1-65535>]

        Updates use persisted SHA-256 snapshots, preserve manual document content,
        rebuild the deterministic backlink index, and rerun Unity analysis only
        for Unity projects. Build writes a static site under <wiki>/site; serve
        binds it only to 127.0.0.1.
        """);
}
