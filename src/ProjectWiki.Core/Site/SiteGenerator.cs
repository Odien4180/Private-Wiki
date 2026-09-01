using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProjectWiki.Core.Config;
using ProjectWiki.Core.Navigation;
using ProjectWiki.Core.Persistence;

namespace ProjectWiki.Core.Site;

internal sealed class SiteGenerator
{
    private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex OrderedListPattern = new(@"^\d+\.\s+(.+)$", RegexOptions.Compiled);

    public WikiBuildResult Build(string wikiRoot)
    {
        var fullWikiRoot = Path.GetFullPath(wikiRoot);
        var siteRoot = Path.Combine(fullWikiRoot, "site");
        var config = AtomicFile.ReadJson<WikiConfig>(Path.Combine(fullWikiRoot, "wiki.config.json"));
        var navigation = new NavigationService().Build(fullWikiRoot);
        var data = new NavigationStore().Load(fullWikiRoot);
        var resolver = new NavigationResolver(data);
        var captions = ReadCaptions(fullWikiRoot);
        var documents = ReadDocuments(fullWikiRoot);
        var documentByEntity = CreateDocumentMap(documents, data, resolver);
        var entityRoutes = data.Entities.Entities
            .GroupBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => documentByEntity.GetValueOrDefault(group.Key)
                    ?? $"entities/{Uri.EscapeDataString(group.Key)}.html",
                StringComparer.OrdinalIgnoreCase);
        var backlinks = data.Backlinks.Backlinks.ToDictionary(
            entry => entry.Target,
            entry => entry.References,
            StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(siteRoot))
        {
            Directory.Delete(siteRoot, recursive: true);
        }

        Directory.CreateDirectory(siteRoot);
        WriteStaticFile(siteRoot, "assets/site.css", SiteCss);
        WriteStaticFile(siteRoot, "assets/site.js", SiteScript);

        foreach (var document in documents)
        {
            var entityId = documentByEntity.FirstOrDefault(pair =>
                string.Equals(pair.Value, document.OutputPath, StringComparison.OrdinalIgnoreCase)).Key;
            var body = RenderMarkdown(document.Content, resolver, entityRoutes, document.OutputPath);
            if (!string.IsNullOrEmpty(entityId))
            {
                body += RenderBacklinks(entityId, document.OutputPath, backlinks, documents);
            }

            body += RenderCaptions(FindCaptions(captions, document.RelativePath, entityId));
            WritePage(siteRoot, document.OutputPath, Layout(
                config,
                document.Title,
                document.OutputPath,
                documents,
                body,
                ExtractTableOfContents(document.Content)));
        }

        foreach (var entity in data.Entities.Entities
                     .GroupBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.OrderBy(entity => entity.Title, StringComparer.Ordinal).First())
                     .OrderBy(entity => entity.Id, StringComparer.Ordinal))
        {
            var outputPath = $"entities/{Uri.EscapeDataString(entity.Id)}.html";
            var body = RenderEntity(entity, outputPath, backlinks, documents, captions);
            WritePage(siteRoot, outputPath, Layout(config, entity.Title, outputPath, documents, body, Array.Empty<TocEntry>()));
        }

        var indexBody = new StringBuilder();
        indexBody.Append("<h1>").Append(Encode(config.Wiki.Title)).AppendLine("</h1>");
        indexBody.AppendLine("<p>Static project wiki generated from Markdown documents.</p>");
        indexBody.AppendLine("<h2>Documents</h2><ul>");
        foreach (var document in documents)
        {
            indexBody.Append("<li><a href=\"")
                .Append(Encode(RelativeUrl("index.html", document.OutputPath)))
                .Append("\">")
                .Append(Encode(document.Title))
                .AppendLine("</a></li>");
        }

        indexBody.AppendLine("</ul>");
        WritePage(siteRoot, "index.html", Layout(config, config.Wiki.Title, "index.html", documents, indexBody.ToString(), Array.Empty<TocEntry>()));

        var searchEntries = CreateSearchEntries(documents, data.Entities.Entities, entityRoutes);
        AtomicFile.WriteJson(Path.Combine(siteRoot, "search-index.json"), new { entries = searchEntries });

        var health = new
        {
            documentCount = documents.Count,
            entityPageCount = data.Entities.Entities.Count,
            searchEntryCount = searchEntries.Count,
            navigation = new
            {
                isValid = navigation.Validation.IsValid,
                issueCount = navigation.Validation.Issues.Count,
                issues = navigation.Validation.Issues.Select(issue => new
                {
                    issue.Code,
                    severity = issue.Severity.ToString().ToLowerInvariant(),
                    issue.Message,
                    issue.DocumentPath,
                    issue.Line,
                    issue.Column,
                }).ToList(),
            },
        };
        AtomicFile.WriteJson(Path.Combine(fullWikiRoot, "reports", "site-health.json"), health);
        WritePage(siteRoot, "health.html", Layout(
            config,
            "Site health",
            "health.html",
            documents,
            RenderHealth(
                health.navigation.isValid,
                health.navigation.issues.Select(issue => (issue.Code, issue.Message))),
            Array.Empty<TocEntry>()));

        return new WikiBuildResult
        {
            WikiRoot = fullWikiRoot,
            SiteRoot = siteRoot,
            DocumentCount = documents.Count,
            EntityPageCount = data.Entities.Entities.Count,
            SearchEntryCount = searchEntries.Count,
            HealthIssueCount = navigation.Validation.Issues.Count,
        };
    }

    private static List<WikiDocument> ReadDocuments(string wikiRoot)
    {
        var root = Path.Combine(wikiRoot, "documents");
        if (!Directory.Exists(root))
        {
            return new List<WikiDocument>();
        }

        return Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
                var outputPath = Path.ChangeExtension(relativePath, ".html").Replace('\\', '/');
                EnsureSafeOutputPath(outputPath);
                var content = File.ReadAllText(path);
                return new WikiDocument(relativePath, outputPath, GetDocumentTitle(content, relativePath), content);
            })
            .ToList();
    }

    private static Dictionary<string, string> CreateDocumentMap(
        IEnumerable<WikiDocument> documents,
        NavigationData data,
        NavigationResolver resolver)
    {
        var candidates = documents
            .Select(document => new
            {
                Document = document,
                Names = new[]
                {
                    Path.GetFileNameWithoutExtension(document.RelativePath),
                    Path.ChangeExtension(document.RelativePath, null) ?? string.Empty,
                },
            })
            .ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in data.Entities.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal))
        {
            var matches = candidates
                .Where(candidate => candidate.Names.Any(name =>
                    string.Equals(name, entity.Id, StringComparison.OrdinalIgnoreCase)
                    || (resolver.Resolve(name).EntityId is { } id
                        && string.Equals(id, entity.Id, StringComparison.OrdinalIgnoreCase))))
                .Select(candidate => candidate.Document.OutputPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (matches.Count == 1)
            {
                result[entity.Id] = matches[0];
            }
        }

        return result;
    }

    private static string RenderEntity(
        ProjectWiki.Core.Model.Entity entity,
        string outputPath,
        IReadOnlyDictionary<string, List<BacklinkReference>> backlinks,
        IReadOnlyList<WikiDocument> documents,
        IReadOnlyList<Caption> captions)
    {
        var content = new StringBuilder();
        content.Append("<h1>").Append(Encode(entity.Title)).AppendLine("</h1>");
        content.Append("<dl class=\"entity-details\">")
            .Append("<dt>Identifier</dt><dd>").Append(Encode(entity.Id)).Append("</dd>")
            .Append("<dt>Type</dt><dd>").Append(Encode(entity.Type.ToString())).AppendLine("</dd></dl>");
        if (entity.Sources.Count > 0)
        {
            content.AppendLine("<h2>Sources</h2><ul>");
            foreach (var source in entity.Sources.OrderBy(source => source, StringComparer.Ordinal))
            {
                content.Append("<li><code>").Append(Encode(source)).AppendLine("</code></li>");
            }

            content.AppendLine("</ul>");
        }

        content.Append(RenderBacklinks(entity.Id, outputPath, backlinks, documents));
        content.Append(RenderCaptions(FindCaptions(captions, entity.Id, entity.Id)));
        return content.ToString();
    }

    private static string RenderBacklinks(
        string entityId,
        string outputPath,
        IReadOnlyDictionary<string, List<BacklinkReference>> backlinks,
        IReadOnlyList<WikiDocument> documents)
    {
        if (!backlinks.TryGetValue(entityId, out var references) || references.Count == 0)
        {
            return string.Empty;
        }

        var byPath = documents.ToDictionary(document => document.RelativePath, StringComparer.OrdinalIgnoreCase);
        var content = new StringBuilder("<section class=\"backlinks\"><h2>Backlinks</h2><ul>");
        foreach (var reference in references
                     .OrderBy(reference => reference.DocumentPath, StringComparer.Ordinal)
                     .ThenBy(reference => reference.Line)
                     .ThenBy(reference => reference.Column))
        {
            if (!byPath.TryGetValue(reference.DocumentPath, out var document))
            {
                continue;
            }

            content.Append("<li><a href=\"")
                .Append(Encode(RelativeUrl(outputPath, document.OutputPath)))
                .Append("\">")
                .Append(Encode(document.Title))
                .Append("</a> <span class=\"location\">line ")
                .Append(reference.Line)
                .AppendLine("</span></li>");
        }

        return content.AppendLine("</ul></section>").ToString();
    }

    private static string RenderCaptions(IEnumerable<Caption> captions)
    {
        var captionList = captions.OrderBy(caption => caption.Id, StringComparer.Ordinal).ToList();
        if (captionList.Count == 0)
        {
            return string.Empty;
        }

        var content = new StringBuilder("<section class=\"source-captions\"><h2>Source captions</h2><ul>");
        foreach (var caption in captionList)
        {
            content.Append("<li data-caption-id=\"").Append(Encode(caption.Id)).Append("\">")
                .Append(Encode(caption.Text));
            if (caption.Source is not null && !string.IsNullOrWhiteSpace(caption.Source.File))
            {
                content.Append(" <span class=\"location\">")
                    .Append(Encode(caption.Source.File));
                if (caption.Source.StartLine is not null)
                {
                    content.Append(':').Append(caption.Source.StartLine);
                    if (caption.Source.EndLine is not null
                        && caption.Source.EndLine != caption.Source.StartLine)
                    {
                        content.Append('–').Append(caption.Source.EndLine);
                    }
                }

                content.Append("</span>");
            }

            content.AppendLine("</li>");
        }

        return content.AppendLine("</ul></section>").ToString();
    }

    private static IEnumerable<Caption> FindCaptions(IEnumerable<Caption> captions, string documentPath, string? entityId)
    {
        var withoutExtension = Path.ChangeExtension(documentPath, null)?.Replace('\\', '/') ?? documentPath;
        var fileName = Path.GetFileNameWithoutExtension(documentPath);
        return captions.Where(caption =>
            string.Equals(caption.Id, documentPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(caption.Id, withoutExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(caption.Id, fileName, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(entityId)
                && string.Equals(caption.Id, entityId, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<Caption> ReadCaptions(string wikiRoot)
    {
        var path = Path.Combine(wikiRoot, "knowledge", "captions.json");
        if (!File.Exists(path))
        {
            return new List<Caption>();
        }

        return JsonSerializer.Deserialize<CaptionIndex>(File.ReadAllText(path), JsonOptions.Default)?.Captions
            ?? new List<Caption>();
    }

    private static string RenderMarkdown(
        string markdown,
        NavigationResolver resolver,
        IReadOnlyDictionary<string, string> entityRoutes,
        string outputPath)
    {
        var content = new StringBuilder();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var headingIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            if (line.StartsWith("<!--", StringComparison.Ordinal) && line.EndsWith("-->", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var language = line[3..].Trim();
                index++;
                var code = new StringBuilder();
                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    code.AppendLine(lines[index++]);
                }

                if (index < lines.Length)
                {
                    index++;
                }

                content.Append("<pre><code");
                if (language.Length > 0)
                {
                    content.Append(" class=\"language-").Append(Encode(language)).Append('"');
                }

                content.Append('>').Append(Encode(code.ToString())).AppendLine("</code></pre>");
                continue;
            }

            var heading = HeadingPattern.Match(line);
            if (heading.Success)
            {
                var level = heading.Groups[1].Length;
                var title = heading.Groups[2].Value;
                var id = CreateHeadingId(PlainText(title), headingIds);
                content.Append("<h").Append(level).Append(" id=\"").Append(Encode(id)).Append("\">")
                    .Append(RenderInline(title, resolver, entityRoutes, outputPath))
                    .Append("</h").Append(level).AppendLine(">");
                index++;
                continue;
            }

            if (IsUnorderedListItem(line))
            {
                content.AppendLine("<ul>");
                do
                {
                    content.Append("<li>").Append(RenderInline(line[2..], resolver, entityRoutes, outputPath)).AppendLine("</li>");
                    index++;
                }
                while (index < lines.Length && IsUnorderedListItem(lines[index]));
                content.AppendLine("</ul>");
                continue;
            }

            var ordered = OrderedListPattern.Match(line);
            if (ordered.Success)
            {
                content.AppendLine("<ol>");
                do
                {
                    content.Append("<li>").Append(RenderInline(OrderedListPattern.Match(lines[index]).Groups[1].Value, resolver, entityRoutes, outputPath)).AppendLine("</li>");
                    index++;
                }
                while (index < lines.Length && OrderedListPattern.IsMatch(lines[index]));
                content.AppendLine("</ol>");
                continue;
            }

            if (line is "---" or "***" or "___")
            {
                content.AppendLine("<hr>");
                index++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            var paragraph = new List<string>();
            while (index < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[index])
                   && !lines[index].StartsWith("```", StringComparison.Ordinal)
                   && !HeadingPattern.IsMatch(lines[index])
                   && !IsUnorderedListItem(lines[index])
                   && !OrderedListPattern.IsMatch(lines[index])
                   && lines[index] is not ("---" or "***" or "___"))
            {
                if (!(lines[index].StartsWith("<!--", StringComparison.Ordinal)
                    && lines[index].EndsWith("-->", StringComparison.Ordinal)))
                {
                    paragraph.Add(lines[index].Trim());
                }

                index++;
            }

            if (paragraph.Count > 0)
            {
                content.Append("<p>").Append(RenderInline(string.Join(" ", paragraph), resolver, entityRoutes, outputPath)).AppendLine("</p>");
            }
        }

        return content.ToString();
    }

    private static string RenderInline(
        string value,
        NavigationResolver resolver,
        IReadOnlyDictionary<string, string> entityRoutes,
        string outputPath)
    {
        var result = new StringBuilder();
        for (var index = 0; index < value.Length;)
        {
            if (value.AsSpan(index).StartsWith("[[", StringComparison.Ordinal))
            {
                var end = value.IndexOf("]]", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    var parts = value[(index + 2)..end].Split('|', 2);
                    var target = parts[0].Trim();
                    var display = parts.Length == 2 ? parts[1].Trim() : target;
                    var resolution = resolver.Resolve(target);
                    if (resolution.Status == NavigationResolutionStatus.Resolved
                        && entityRoutes.TryGetValue(resolution.EntityId!, out var route))
                    {
                        result.Append("<a class=\"wiki-link\" href=\"")
                            .Append(Encode(RelativeUrl(outputPath, route)))
                            .Append("\">")
                            .Append(Encode(display))
                            .Append("</a>");
                    }
                    else
                    {
                        result.Append("<span class=\"wiki-link unresolved\">")
                            .Append(Encode(display))
                            .Append("</span>");
                    }

                    index = end + 2;
                    continue;
                }
            }

            if (value[index] == '`')
            {
                var end = value.IndexOf('`', index + 1);
                if (end >= 0)
                {
                    result.Append("<code>").Append(Encode(value[(index + 1)..end])).Append("</code>");
                    index = end + 1;
                    continue;
                }
            }

            if (value[index] == '[')
            {
                var labelEnd = value.IndexOf("](", index + 1, StringComparison.Ordinal);
                if (labelEnd > index)
                {
                    var urlEnd = value.IndexOf(')', labelEnd + 2);
                    if (urlEnd >= 0)
                    {
                        var label = value[(index + 1)..labelEnd];
                        var url = value[(labelEnd + 2)..urlEnd];
                        if (IsSafeMarkdownUrl(url))
                        {
                            result.Append("<a href=\"").Append(Encode(url.Trim())).Append("\">")
                                .Append(RenderInline(label, resolver, entityRoutes, outputPath)).Append("</a>");
                        }
                        else
                        {
                            result.Append(RenderInline(label, resolver, entityRoutes, outputPath));
                        }

                        index = urlEnd + 1;
                        continue;
                    }
                }
            }

            if (value.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
            {
                var end = value.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    result.Append("<strong>").Append(RenderInline(value[(index + 2)..end], resolver, entityRoutes, outputPath)).Append("</strong>");
                    index = end + 2;
                    continue;
                }
            }

            if (value[index] == '*')
            {
                var end = value.IndexOf('*', index + 1);
                if (end > index + 1)
                {
                    result.Append("<em>").Append(RenderInline(value[(index + 1)..end], resolver, entityRoutes, outputPath)).Append("</em>");
                    index = end + 1;
                    continue;
                }
            }

            result.Append(Encode(value[index].ToString()));
            index++;
        }

        return result.ToString();
    }

    private static IReadOnlyList<TocEntry> ExtractTableOfContents(string markdown)
    {
        var headingIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return markdown.ReplaceLineEndings("\n").Split('\n')
            .Select(HeadingPattern.Match)
            .Where(match => match.Success && match.Groups[1].Length > 1)
            .Select(match => new TocEntry(
                match.Groups[1].Length,
                PlainText(match.Groups[2].Value),
                CreateHeadingId(PlainText(match.Groups[2].Value), headingIds)))
            .ToList();
    }

    private static string Layout(
        WikiConfig config,
        string pageTitle,
        string outputPath,
        IReadOnlyList<WikiDocument> documents,
        string body,
        IReadOnlyList<TocEntry> tableOfContents)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"");
        html.Append(Encode(config.Wiki.Language)).Append("\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<title>").Append(Encode(pageTitle)).Append(" · ").Append(Encode(config.Wiki.Title)).Append("</title>")
            .Append("<link rel=\"stylesheet\" href=\"").Append(Encode(RelativeUrl(outputPath, "assets/site.css"))).Append("\">")
            .Append("</head><body><header><a href=\"").Append(Encode(RelativeUrl(outputPath, "index.html"))).Append("\">")
            .Append(Encode(config.Wiki.Title)).Append("</a></header><div class=\"layout\">");
        html.Append("<aside><h2>Sidebar</h2><nav><ul>");
        foreach (var document in documents)
        {
            html.Append("<li><a href=\"").Append(Encode(RelativeUrl(outputPath, document.OutputPath))).Append("\">")
                .Append(Encode(document.Title)).Append("</a></li>");
        }

        html.Append("<li><a href=\"").Append(Encode(RelativeUrl(outputPath, "health.html"))).Append("\">Site health</a></li>")
            .Append("</ul></nav></aside><main><label class=\"search\"><span>Search</span><input id=\"search\" type=\"search\" autocomplete=\"off\"></label>")
            .Append("<ul id=\"search-results\"></ul>")
            .Append(body)
            .Append("</main>");
        if (tableOfContents.Count > 0)
        {
            html.Append("<aside class=\"toc\"><h2>On this page</h2><ol>");
            foreach (var entry in tableOfContents)
            {
                html.Append("<li class=\"toc-level-").Append(entry.Level).Append("\"><a href=\"#")
                    .Append(Encode(entry.Id)).Append("\">").Append(Encode(entry.Text)).Append("</a></li>");
            }

            html.Append("</ol></aside>");
        }

        return html.Append("</div><script data-site-root=\"")
            .Append(Encode(RelativeUrl(outputPath, "index.html")))
            .Append("\" src=\"")
            .Append(Encode(RelativeUrl(outputPath, "assets/site.js")))
            .Append("\"></script></body></html>")
            .ToString();
    }

    private static string RenderHealth(bool isValid, IEnumerable<(string Code, string Message)> issues)
    {
        var issueList = issues.ToList();
        var content = new StringBuilder("<h1>Site health</h1><p class=\"");
        content.Append(isValid ? "healthy" : "unhealthy").Append("\">")
            .Append(isValid ? "Navigation is healthy." : $"{issueList.Count} navigation issue(s) found.")
            .Append("</p>");
        if (issueList.Count > 0)
        {
            content.Append("<ul class=\"health-issues\">");
            foreach (var issue in issueList)
            {
                content.Append("<li><code>").Append(Encode(issue.Code)).Append("</code> ")
                    .Append(Encode(issue.Message)).Append("</li>");
            }

            content.Append("</ul>");
        }

        return content.ToString();
    }

    private static List<SearchEntry> CreateSearchEntries(
        IEnumerable<WikiDocument> documents,
        IEnumerable<ProjectWiki.Core.Model.Entity> entities,
        IReadOnlyDictionary<string, string> entityRoutes) =>
        documents.Select(document => new SearchEntry
            {
                Title = document.Title,
                Url = document.OutputPath,
                Text = PlainText(document.Content),
            })
            .Concat(entities.Select(entity => new SearchEntry
            {
                Title = entity.Title,
                Url = entityRoutes[entity.Id],
                Text = string.Join(" ", new[] { entity.Id, entity.Type.ToString() }
                    .Concat(entity.Sources)
                    .Concat(entity.Symbols)
                    .Concat(entity.Members)),
            }))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

    private static string GetDocumentTitle(string content, string relativePath)
    {
        var match = content.ReplaceLineEndings("\n").Split('\n').Select(HeadingPattern.Match)
            .FirstOrDefault(candidate => candidate.Success && candidate.Groups[1].Length == 1);
        return match is null ? Path.GetFileNameWithoutExtension(relativePath) : PlainText(match.Groups[2].Value);
    }

    private static string PlainText(string text)
    {
        var result = new StringBuilder();
        for (var index = 0; index < text.Length;)
        {
            if (text.AsSpan(index).StartsWith("[[", StringComparison.Ordinal))
            {
                var end = text.IndexOf("]]", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    var parts = text[(index + 2)..end].Split('|', 2);
                    result.Append(parts.Length == 2 ? parts[1].Trim() : parts[0].Trim());
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] is '*' or '`' or '#' or '[' or ']')
            {
                index++;
                continue;
            }

            result.Append(text[index++]);
        }

        return result.ToString().Trim();
    }

    private static string CreateHeadingId(string value, IDictionary<string, int> existing)
    {
        var normalized = new StringBuilder();
        var previousHyphen = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(character);
                previousHyphen = false;
            }
            else if (!previousHyphen)
            {
                normalized.Append('-');
                previousHyphen = true;
            }
        }

        var id = normalized.ToString().Trim('-');
        if (id.Length == 0)
        {
            id = "section";
        }

        existing.TryGetValue(id, out var count);
        existing[id] = count + 1;
        return count == 0 ? id : $"{id}-{count + 1}";
    }

    private static bool IsUnorderedListItem(string line) =>
        line.Length > 2 && (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal));

    private static bool IsSafeMarkdownUrl(string value)
    {
        var url = value.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal)
            || url.Contains('\\')
            || url.Contains('\0'))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        return !uri.IsAbsoluteUri
            || uri.Scheme is Uri.UriSchemeHttp or Uri.UriSchemeHttps or "mailto";
    }

    private static string RelativeUrl(string fromOutputPath, string toOutputPath)
    {
        var fromDirectory = Path.GetDirectoryName(fromOutputPath);
        if (string.IsNullOrEmpty(fromDirectory))
        {
            fromDirectory = ".";
        }

        var relative = Path.GetRelativePath(fromDirectory, toOutputPath).Replace('\\', '/');
        return relative == "." ? Path.GetFileName(toOutputPath) : relative;
    }

    private static void WritePage(string siteRoot, string relativePath, string content)
    {
        EnsureSafeOutputPath(relativePath);
        WriteStaticFile(siteRoot, relativePath, content);
    }

    private static void WriteStaticFile(string siteRoot, string relativePath, string content)
    {
        EnsureSafeOutputPath(relativePath);
        var path = Path.GetFullPath(Path.Combine(siteRoot, relativePath));
        if (!IsWithin(siteRoot, path))
        {
            throw new InvalidDataException($"Generated site path escapes the site root: {relativePath}");
        }

        AtomicFile.WriteText(path, content);
    }

    private static void EnsureSafeOutputPath(string path)
    {
        if (Path.IsPathRooted(path)
            || path.Split('/', '\\').Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"Invalid generated site path: {path}");
        }
    }

    private static bool IsWithin(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private sealed record WikiDocument(string RelativePath, string OutputPath, string Title, string Content);

    private sealed record TocEntry(int Level, string Text, string Id);

    private sealed class SearchEntry
    {
        public required string Title { get; init; }

        public required string Url { get; init; }

        public required string Text { get; init; }
    }

    private sealed class CaptionIndex
    {
        public List<Caption> Captions { get; init; } = new();
    }

    private sealed class Caption
    {
        public string Id { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public CaptionSource? Source { get; init; }
    }

    private sealed class CaptionSource
    {
        public string? File { get; init; }

        public int? StartLine { get; init; }

        public int? EndLine { get; init; }
    }

    private const string SiteCss = """
        :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
        body { margin: 0; line-height: 1.5; }
        header { padding: 1rem 2rem; border-bottom: 1px solid #8885; font-weight: 700; }
        header a { color: inherit; text-decoration: none; }
        .layout { display: grid; grid-template-columns: minmax(13rem, 18rem) minmax(0, 1fr) minmax(12rem, 16rem); gap: 2rem; max-width: 90rem; margin: auto; padding: 2rem; }
        main { min-width: 0; } aside ul, .toc ol { padding-left: 1.2rem; } .toc { font-size: .9rem; } .toc-level-3 { margin-left: .75rem; } .toc-level-4, .toc-level-5, .toc-level-6 { margin-left: 1.5rem; }
        pre { overflow: auto; padding: 1rem; background: #8882; } code { font-family: ui-monospace, monospace; } .search { display: block; margin-bottom: 1rem; } .search input { margin-left: .5rem; }
        #search-results:empty { display: none; } #search-results { padding-left: 1.2rem; } .unresolved { text-decoration: underline dotted; } .location { color: #777; font-size: .9em; } .healthy { color: #18794e; } .unhealthy { color: #b42318; }
        @media (max-width: 50rem) { .layout { display: block; } aside, .toc { margin: 1.5rem 0; } }
        """;

    private const string SiteScript = """
        (() => {
          const input = document.getElementById("search");
          const results = document.getElementById("search-results");
          if (!input || !results) return;
          let entries = [];
          const script = document.querySelector("script[data-site-root]");
          const siteRoot = new URL(script?.dataset.siteRoot || "index.html", document.baseURI);
          fetch(new URL("search-index.json", siteRoot))
            .then(response => response.ok ? response.json() : { entries: [] })
            .then(index => { entries = Array.isArray(index.entries) ? index.entries : []; });
          input.addEventListener("input", () => {
            const query = input.value.trim().toLowerCase();
            results.replaceChildren();
            if (!query) return;
            entries.filter(entry => `${entry.title} ${entry.text}`.toLowerCase().includes(query)).slice(0, 20).forEach(entry => {
              const item = document.createElement("li");
              const link = document.createElement("a");
              link.href = new URL(entry.url, siteRoot).href;
              link.textContent = entry.title;
              item.append(link);
              results.append(item);
            });
          });
        })();
        """;
}
