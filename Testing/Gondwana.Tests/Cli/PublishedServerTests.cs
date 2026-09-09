using System.Net;
using System.Net.Sockets;
using Gondwana.Cli.Commands;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

[Collection("Global engine state")]
public sealed class PublishedServerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GondwanaServe_" + Guid.NewGuid().ToString("N"));
    public PublishedServerTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void Discovery_RequiresPublishedOutput_AndSupportsRuntimeSubdirectory()
    {
        var project = Path.Combine(root, "Game.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"><PropertyGroup><TargetFramework>net8.0-browser</TargetFramework></PropertyGroup></Project>");
        var build = Path.Combine(root, "bin", "Release", "net8.0-browser", "wwwroot");
        Directory.CreateDirectory(build);
        File.WriteAllText(Path.Combine(build, "index.html"), "build output");
        Assert.Null(ProjectHelper.TryLocateBlazorPublishRoot(project, "Release"));
        var exception = Assert.Throws<InvalidOperationException>(() => ServeCommand.ResolveRoot(new() { Project = project }));
        Assert.Contains("gondwana publish blazor", exception.Message);
        var app = new CommandApp();
        app.Configure(c => c.AddCommand<ServeCommand>("serve"));
        Assert.Equal(1, app.Run(["serve", "-p", project, "--no-open"]));
        var published = Path.Combine(root, "bin", "Release", "net8.0-browser", "browser-wasm", "publish", "wwwroot");
        Directory.CreateDirectory(published);
        File.WriteAllText(Path.Combine(published, "index.html"), "published");
        Assert.Equal(published, ServeCommand.ResolveRoot(new() { Project = project }));
        Assert.Equal(published, ServeCommand.ResolveRoot(new() { Root = published }));
    }

    [Fact]
    public async Task Server_SendsIsolationAndMimeHeaders_HandlesHeadAndBasePath_StopsCleanly()
    {
        File.WriteAllText(Path.Combine(root, "index.html"), "<base href=\"/game/\">fixture");
        File.WriteAllBytes(Path.Combine(root, "test.wasm"), [0, 97, 115, 109]);
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = PublishedServer.Run(root, port, url => ready.SetResult(url), stop.Token);
        try
        {
            var first = await Task.WhenAny(server, ready.Task);
            if (first == server) await server; // Preserve bind failure details.
            var url = await ready.Task.WaitAsync(stop.Token);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(url + "test.wasm");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/wasm", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
            Assert.Equal("require-corp", response.Headers.GetValues("Cross-Origin-Embedder-Policy").Single());
            Assert.Equal(new byte[] { 0, 97, 115, 109 }, await response.Content.ReadAsByteArrayAsync());
            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url + "test.wasm"));
            Assert.Equal(4L, head.Content.Headers.ContentLength);
            Assert.Empty(await head.Content.ReadAsByteArrayAsync());
            using var missing = await client.GetAsync(url + "missing.wasm");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.True(missing.Headers.Contains("Cross-Origin-Embedder-Policy"));
            using var unsafePath = await client.GetAsync(url + "%2e%2e%5csecret.txt");
            Assert.Equal(HttpStatusCode.BadRequest, unsafePath.StatusCode);
        }
        finally { stop.Cancel(); await server.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
}
