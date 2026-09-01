using System.Text.Json;
using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Persistence;

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
    case "update":
        return RunUpdate(options, isRebuild: false);
    case "rebuild":
        return RunUpdate(options, isRebuild: true);
    case "inspect":
        return RunInspect(GetInspectEntity(commandArguments), options);
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

static int RunUpdate(Dictionary<string, string> options, bool isRebuild)
{
    var command = isRebuild ? "rebuild" : "update";
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
        Console.Error.WriteLine($"{command} failed: {ex.Message}");
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

        result[name] = value;
    }

    return result;
}

static void PrintUsage()
{
    Console.WriteLine("""
        project-wiki - reusable project wiki engine (Milestone 4: incremental updates)

        Usage:
          project-wiki init --project <path> --wiki <path> [--title <title>] [--language <lang>]
          project-wiki update --wiki <path>
          project-wiki rebuild --wiki <path>
          project-wiki inspect <entity> --wiki <path>

        Updates use persisted SHA-256 snapshots, preserve manual document content,
        and rebuild the deterministic backlink index.
        Not yet implemented (planned milestones): validate, build, serve.
        """);
}
