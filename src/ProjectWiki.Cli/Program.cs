using System.Text.Json;
using ProjectWiki.Core.Engine;
using ProjectWiki.Core.Persistence;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0];
var options = ParseOptions(args.Skip(1).ToArray());

switch (command)
{
    case "init":
        return RunInit(options);
    case "--help":
    case "-h":
    case "help":
        PrintUsage();
        return 0;
    default:
        Console.Error.WriteLine($"Unknown or not-yet-implemented command: '{command}'.");
        Console.Error.WriteLine("Only 'init' is implemented in Milestone 1. See .agents/skills/project-wiki/docs/architecture.md.");
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
        var engine = new WikiEngine();
        var result = engine.Init(new WikiInitOptions
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
        project-wiki - reusable project wiki engine (Milestone 1: init only)

        Usage:
          project-wiki init --project <path> --wiki <path> [--title <title>] [--language <lang>]

        Not yet implemented (planned milestones): update, inspect, validate, rebuild, serve.
        """);
}
