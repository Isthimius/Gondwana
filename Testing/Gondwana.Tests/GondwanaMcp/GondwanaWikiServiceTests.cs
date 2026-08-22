using Gondwana.Mcp.Configuration;
using Gondwana.Mcp.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Gondwana.Tests.GondwanaMcp;

public sealed class GondwanaWikiServiceTests
{
    [Fact]
    public async Task ListPages_OnlyDiscoversConfiguredGondwanaWiki()
    {
        const string html =
            "<html>" +
            "<a href=\"/Isthimius/Gondwana/wiki/Collision-Detection\">Collision Detection</a>" +
            "<a href=\"/Isthimius/Gondwana/wiki/Rendering-Pipeline\"><span>Rendering Pipeline</span></a>" +
            "<a href=\"/someone/else/wiki/Secret\">Secret</a>" +
            "</html>";

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(_ =>
                TestHttpMessageHandler.Text(html, "text/html")));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = new GondwanaWikiService(
            httpClient,
            cache,
            Options.Create(new GondwanaMcpOptions()));

        var result = await service.ListPagesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result.Pages, page => page.Slug == "Collision-Detection");
        Assert.Contains(result.Pages, page => page.Slug == "Rendering-Pipeline");
        Assert.DoesNotContain(result.Pages, page => page.Slug == "Secret");
    }

    [Fact]
    public async Task ReadPage_ForeignWikiUrlIsReducedToGondwanaSlug()
    {
        Uri? requestedUri = null;

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(request =>
            {
                requestedUri = request.RequestUri;
                return TestHttpMessageHandler.Text(
                    "# Collision Detection\n\nGondwana collision documentation.");
            }));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = new GondwanaWikiService(
            httpClient,
            cache,
            Options.Create(new GondwanaMcpOptions()));

        var result = await service.ReadPageAsync(
            "https://github.com/someone/other/wiki/Collision-Detection");

        Assert.NotNull(requestedUri);
        Assert.Equal("raw.githubusercontent.com", requestedUri!.Host);
        Assert.Equal(
            "/wiki/Isthimius/Gondwana/Collision-Detection.md",
            requestedUri.AbsolutePath);
        Assert.Equal("Collision Detection", result.Title);
        Assert.Contains("Gondwana collision documentation", result.Markdown);
    }

    [Fact]
    public async Task SearchWiki_SearchesMarkdownAndRanksMatches()
    {
        const string pagesHtml =
            "<a href=\"/Isthimius/Gondwana/wiki/Collision-Detection\">Collision Detection</a>" +
            "<a href=\"/Isthimius/Gondwana/wiki/Logging\">Logging</a>";

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(request =>
            {
                string uri = request.RequestUri!.ToString();

                if (uri.EndsWith("/wiki/_pages", StringComparison.Ordinal))
                {
                    return TestHttpMessageHandler.Text(pagesHtml, "text/html");
                }

                if (uri.EndsWith("/Collision-Detection.md", StringComparison.Ordinal))
                {
                    return TestHttpMessageHandler.Text(
                        "# Collision Detection\n\nCollision groups, masks, and collider behavior.");
                }

                if (uri.EndsWith("/Logging.md", StringComparison.Ordinal))
                {
                    return TestHttpMessageHandler.Text(
                        "# Logging\n\nStructured engine logging and event IDs.");
                }

                return TestHttpMessageHandler.Text(
                    string.Empty,
                    statusCode: System.Net.HttpStatusCode.NotFound);
            }));

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var service = new GondwanaWikiService(
            httpClient,
            cache,
            Options.Create(new GondwanaMcpOptions()));

        var result = await service.SearchAsync("collision");

        Assert.Equal(2, result.ScannedPages);
        Assert.NotEmpty(result.Matches);
        Assert.Equal("Collision-Detection", result.Matches[0].Slug);
        Assert.Contains(
            "Collision",
            result.Matches[0].Snippet,
            StringComparison.OrdinalIgnoreCase);
    }
}
