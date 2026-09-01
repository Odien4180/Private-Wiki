using System.Net;

namespace ProjectWiki.Core.Site;

internal sealed class LocalSiteServer
{
    public WikiServeResult Serve(string wikiRoot, int port, CancellationToken cancellationToken)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        var build = new SiteGenerator().Build(wikiRoot);
        var siteRoot = Path.GetFullPath(build.SiteRoot);
        var url = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContextAsync().WaitAsync(cancellationToken).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                WriteResponse(context, siteRoot);
            }
        }
        finally
        {
            listener.Stop();
        }

        return new WikiServeResult { Url = url, Build = build };
    }

    private static void WriteResponse(HttpListenerContext context, string siteRoot)
    {
        try
        {
            if (context.Request.HttpMethod is not ("GET" or "HEAD"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var requestedPath = context.Request.Url?.AbsolutePath ?? "/";
            if (!TryGetFilePath(siteRoot, requestedPath, out var filePath) || !File.Exists(filePath))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            context.Response.ContentType = GetContentType(filePath);
            context.Response.ContentLength64 = new FileInfo(filePath).Length;
            context.Response.AddHeader("X-Content-Type-Options", "nosniff");
            if (context.Request.HttpMethod == "GET")
            {
                using var input = File.OpenRead(filePath);
                input.CopyTo(context.Response.OutputStream);
            }
        }
        catch (IOException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static bool TryGetFilePath(string siteRoot, string requestedPath, out string filePath)
    {
        filePath = string.Empty;
        string decodedPath;
        try
        {
            decodedPath = Uri.UnescapeDataString(requestedPath);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decodedPath.Contains('\0') || decodedPath.Contains('\\'))
        {
            return false;
        }

        var relativePath = decodedPath.TrimStart('/');
        if (relativePath.Length == 0 || relativePath.EndsWith("/", StringComparison.Ordinal))
        {
            relativePath += "index.html";
        }

        if (relativePath.Split('/', StringSplitOptions.None).Any(segment => segment is "." or ".."))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(siteRoot, relativePath));
        if (!IsWithin(siteRoot, candidate))
        {
            return false;
        }

        filePath = candidate;
        return true;
    }

    private static bool IsWithin(string parent, string child)
    {
        var relative = Path.GetRelativePath(parent, child);
        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string GetContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        _ => "application/octet-stream",
    };
}
