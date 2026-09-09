using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using Gondwana.Cli.Commands.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal sealed class ServeCommand : AsyncCommand<ServeCommand.Settings>
{
    public sealed class Settings : ProjectSettings
    {
        [CommandOption("--port <PORT>")]
        [Description("Local HTTP port.")]
        [DefaultValue(5000)]
        public int Port { get; init; } = 5000;

        [CommandOption("--no-open")]
        [Description("Do not launch the browser.")]
        public bool NoOpen { get; init; }

        [CommandOption("-c|--configuration <NAME>")]
        [Description("Configuration of the existing published output.")]
        [DefaultValue("Release")]
        public string Configuration { get; init; } = "Release";

        [CommandOption("-f|--framework <TFM>")]
        [Description("Published browser target framework (auto-detected when unambiguous).")]
        public string? Framework { get; init; }

        [CommandOption("--root <PATH>")]
        [Description("Explicit published wwwroot, for custom publish output paths.")]
        public string? Root { get; init; }
    }

    internal static string ResolveRoot(Settings settings)
    {
        if (settings.Root is not null)
        {
            if (settings.Project is not null) throw new ArgumentException("Choose --root or --project, not both.");
            var root = Path.GetFullPath(settings.Root);
            if (!File.Exists(Path.Combine(root, "index.html"))) throw new InvalidOperationException("--root must contain a published index.html.");
            return root;
        }
        if (!ProjectHelper.TryResolveProject(settings.Project, out var project, out var error)) throw new InvalidOperationException(error);
        if (!ProjectHelper.TryResolveBrowserFramework(project!, settings.Framework, out var framework, out error)) throw new InvalidOperationException(error);
        return ProjectHelper.TryLocateBlazorPublishRoot(project!, settings.Configuration, framework)
            ?? throw new InvalidOperationException("No published browser wwwroot found. Run 'gondwana publish blazor' first, or specify --root for custom output.");
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            if (settings.Port is < 1 or > 65535) throw new ArgumentException("Port must be between 1 and 65535.");
            var root = ResolveRoot(settings);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; stop.Cancel(); };
            Console.CancelKeyPress += handler;
            try
            {
                await PublishedServer.Run(root, settings.Port, url =>
                {
                    AnsiConsole.WriteLine($"Serving {root} at {url} (Ctrl+C to stop)");
                    if (!settings.NoOpen) ProcessHelper.OpenBrowser(url);
                }, stop.Token);
            }
            finally { Console.CancelKeyPress -= handler; }
            return 0;
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}

internal static class PublishedServer
{
    internal static string BasePath(string root)
    {
        var html = File.ReadAllText(Path.Combine(root, "index.html"));
        var match = Regex.Match(html, "<base\\b[^>]*\\bhref\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
        var value = match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : "/";
        if (value is "" or "." or "./" or "/") return "/";
        if (!value.StartsWith('/') || value.StartsWith("//") || value.Contains('?') || value.Contains('#'))
            throw new InvalidOperationException("Published base href must be '/' or a local absolute path. Republish with --base-href / for local serving.");
        SafeFilePath.ValidateRelative(Uri.UnescapeDataString(value.Trim('/')));
        return value.TrimEnd('/') + "/";
    }

    public static async Task Run(string root, int port, Action<string> ready, CancellationToken cancellationToken)
    {
        var basePath = BasePath(root);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        using var registration = cancellationToken.Register(listener.Stop);
        var requests = new List<Task>();
        try
        {
            ready($"http://localhost:{port}{basePath}");
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                requests.RemoveAll(t => t.IsCompleted);
                requests.Add(Respond(context, root, basePath, cancellationToken));
            }
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is HttpListenerException or ObjectDisposedException or OperationCanceledException) { }
        finally
        {
            listener.Stop();
            await Task.WhenAll(requests);
        }
    }

    private static async Task Respond(HttpListenerContext context, string root, string basePath, CancellationToken cancellationToken)
    {
        var response = context.Response;
        try
        {
            response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["Cache-Control"] = "no-cache";
            if (context.Request.RemoteEndPoint is not { } remote || !IPAddress.IsLoopback(remote.Address)) { response.StatusCode = 403; return; }
            if (context.Request.HttpMethod is not ("GET" or "HEAD")) { response.StatusCode = 405; return; }
            var urlPath = context.Request.Url!.AbsolutePath;
            if (urlPath == "/" && basePath != "/") { response.Redirect(basePath); return; }
            if (!urlPath.StartsWith(basePath, StringComparison.Ordinal)) { response.StatusCode = 404; return; }
            var name = Uri.UnescapeDataString(urlPath[basePath.Length..]);
            if (name.Length == 0) name = "index.html";
            var path = SafeFilePath.Resolve(root, name);
            if (!File.Exists(path)) { response.StatusCode = 404; return; }
            response.ContentType = ContentType(path);
            using var stream = File.OpenRead(path);
            response.ContentLength64 = stream.Length;
            if (context.Request.HttpMethod == "GET") await stream.CopyToAsync(response.OutputStream, cancellationToken);
        }
        catch (InvalidDataException) { response.StatusCode = 400; }
        catch (UnauthorizedAccessException) { response.StatusCode = 403; }
        catch (IOException) { response.StatusCode = 500; }
        catch (Exception ex) when (ex is HttpListenerException or OperationCanceledException or ObjectDisposedException) { }
        finally { response.Close(); }
    }

    internal static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8", ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8", ".json" or ".map" => "application/json",
        ".wasm" => "application/wasm", ".svg" => "image/svg+xml", ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", ".ico" => "image/x-icon",
        ".woff" => "font/woff", ".woff2" => "font/woff2", ".ttf" => "font/ttf", ".mp3" => "audio/mpeg", ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };
}
