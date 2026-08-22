using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Gondwana.Mcp.Configuration;
using Gondwana.Mcp.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Gondwana.Mcp.Services;

public sealed class GondwanaWikiService
{
    private const string PagesCacheKey = "gondwana-wiki-pages";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly GondwanaMcpOptions _options;

    public GondwanaWikiService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GondwanaMcpOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Gondwana-MCP/1.0");
    }

    public async Task<WikiPageListResult> ListPagesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WikiPageSummaryResult> pages =
            await GetPageSummariesAsync(cancellationToken);

        return new WikiPageListResult(pages.Count, pages);
    }

    public async Task<WikiPageResult> ReadPageAsync(
        string page,
        CancellationToken cancellationToken = default)
    {
        string slug = NormalizeWikiSlug(page);
        string cacheKey = "gondwana-wiki-page:" + slug.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out WikiPageResult? cached) &&
            cached is not null)
        {
            return cached;
        }

        string markdown = await DownloadWikiMarkdownAsync(slug, cancellationToken);
        string title = ExtractTitle(markdown, slug);

        var result = new WikiPageResult(
            title,
            slug,
            BuildWikiPageUrl(slug),
            markdown);

        _cache.Set(
            cacheKey,
            result,
            TimeSpan.FromMinutes(_options.WikiCacheMinutes));

        return result;
    }

    public async Task<WikiSearchResult> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = (query ?? string.Empty).Trim();

        if (normalizedQuery.Length == 0)
        {
            throw new ArgumentException("A wiki-search query is required.", nameof(query));
        }

        if (normalizedQuery.Length > 250)
        {
            throw new ArgumentException(
                "Wiki-search queries are limited to 250 characters.",
                nameof(query));
        }

        int resultLimit = Math.Clamp(maxResults, 1, _options.MaxSearchResults);

        IReadOnlyList<WikiPageSummaryResult> pages =
            await GetPageSummariesAsync(cancellationToken);

        var matches = new ConcurrentBag<WikiSearchMatchResult>();

        await Parallel.ForEachAsync(
            pages,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _options.WikiSearchConcurrency
            },
            async (page, token) =>
            {
                try
                {
                    WikiPageResult content = await ReadPageAsync(page.Slug, token);
                    int score = Score(content, normalizedQuery);

                    if (score > 0)
                    {
                        matches.Add(new WikiSearchMatchResult(
                            content.Title,
                            content.Slug,
                            content.Url,
                            score,
                            BuildSnippet(content.Markdown, normalizedQuery)));
                    }
                }
                catch (KeyNotFoundException)
                {
                    // A page can be renamed between discovery and content fetch.
                    // Skip it; the next cache refresh will rediscover the wiki.
                }
            });

        IReadOnlyList<WikiSearchMatchResult> ordered = matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToArray();

        return new WikiSearchResult(
            normalizedQuery,
            pages.Count,
            ordered);
    }

    private async Task<IReadOnlyList<WikiPageSummaryResult>> GetPageSummariesAsync(
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(
                PagesCacheKey,
                out IReadOnlyList<WikiPageSummaryResult>? cached) &&
            cached is not null)
        {
            return cached;
        }

        string pagesUrl =
            $"https://github.com/{GondwanaMcpOptions.RepositoryFullName}/wiki/_pages";

        using var request = new HttpRequestMessage(HttpMethod.Get, pagesUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync(cancellationToken);

        string prefix =
            $"/{GondwanaMcpOptions.RepositoryOwner}/{GondwanaMcpOptions.RepositoryName}/wiki/";

        string pattern =
            $"href=\"(?:https://github\\.com)?{Regex.Escape(prefix)}(?<slug>[^\"#?]+)\"[^>]*>(?<title>.*?)</a>";

        var pages = new Dictionary<string, WikiPageSummaryResult>(
            StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
                     html,
                     pattern,
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string slug = WebUtility.HtmlDecode(
                Uri.UnescapeDataString(match.Groups["slug"].Value));

            if (slug is "_pages" or "_new" ||
                slug.StartsWith("_history", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string rawTitle = Regex.Replace(
                match.Groups["title"].Value,
                "<[^>]+>",
                string.Empty);

            string title = WebUtility.HtmlDecode(rawTitle).Trim();

            if (title.Length == 0)
            {
                title = HumanizeSlug(slug);
            }

            pages[slug] = new WikiPageSummaryResult(
                title,
                slug,
                BuildWikiPageUrl(slug));
        }

        IReadOnlyList<WikiPageSummaryResult> result = pages.Values
            .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                "No Gondwana wiki pages were discovered. GitHub may have changed the wiki page-list markup.");
        }

        _cache.Set(
            PagesCacheKey,
            result,
            TimeSpan.FromMinutes(_options.WikiCacheMinutes));

        return result;
    }

    private async Task<string> DownloadWikiMarkdownAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        string escapedSlug = EscapePath(slug);

        string rawUrl =
            $"https://raw.githubusercontent.com/wiki/{GondwanaMcpOptions.RepositoryOwner}/{GondwanaMcpOptions.RepositoryName}/{escapedSlug}.md";

        string? markdown = await TryDownloadMarkdownAsync(
            rawUrl,
            cancellationToken);

        if (markdown is null)
        {
            string fallbackUrl = $"{BuildWikiPageUrl(slug)}.md";

            markdown = await TryDownloadMarkdownAsync(
                fallbackUrl,
                cancellationToken);
        }

        if (markdown is null)
        {
            throw new KeyNotFoundException(
                $"Wiki page '{slug}' was not found in the Gondwana wiki.");
        }

        if (Encoding.UTF8.GetByteCount(markdown) > _options.MaxFileBytes)
        {
            throw new InvalidOperationException(
                $"Wiki page '{slug}' exceeds the MCP read limit of {_options.MaxFileBytes:N0} bytes.");
        }

        return markdown;
    }

    private async Task<string?> TryDownloadMarkdownAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        string mediaType =
            response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        string body =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (mediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) &&
            body.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return body;
    }

    private static int Score(WikiPageResult page, string query)
    {
        int score = 0;

        if (page.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (page.Slug.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
        }

        int fullQueryOccurrences = CountOccurrences(page.Markdown, query);
        score += Math.Min(fullQueryOccurrences, 10) * 10;

        string[] tokens = Regex.Split(query, @"\s+")
            .Where(token => token.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string token in tokens)
        {
            if (page.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
            }

            score += Math.Min(CountOccurrences(page.Markdown, token), 5);
        }

        return score;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(
                    value,
                    index,
                    StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += Math.Max(value.Length, 1);
        }

        return count;
    }

    private static string BuildSnippet(string markdown, string query)
    {
        int index = markdown.IndexOf(
            query,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            string? token = Regex.Split(query, @"\s+")
                .FirstOrDefault(value =>
                    value.Length > 1 &&
                    markdown.Contains(value, StringComparison.OrdinalIgnoreCase));

            index = token is null
                ? 0
                : markdown.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        }

        int start = Math.Max(0, index - 140);
        int length = Math.Min(420, markdown.Length - start);

        string snippet = markdown.Substring(start, length);
        snippet = Regex.Replace(snippet, @"\s+", " ").Trim();

        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (start + length < markdown.Length)
        {
            snippet += "…";
        }

        return snippet;
    }

    private static string NormalizeWikiSlug(string page)
    {
        string normalized = (page ?? string.Empty).Trim();

        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "A Gondwana wiki page is required.",
                nameof(page));
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
        {
            const string marker = "/wiki/";

            int markerIndex = uri.AbsolutePath.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

            normalized = markerIndex >= 0
                ? uri.AbsolutePath[(markerIndex + marker.Length)..]
                : uri.AbsolutePath.Trim('/');
        }

        normalized = Uri.UnescapeDataString(normalized)
            .Trim('/')
            .Replace(' ', '-');

        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        if (normalized.Length == 0 || normalized.Length > 300)
        {
            throw new ArgumentException(
                "The Gondwana wiki page name is invalid.",
                nameof(page));
        }

        string[] segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                "Wiki page names cannot contain traversal segments.",
                nameof(page));
        }

        return string.Join('/', segments);
    }

    private static string ExtractTitle(string markdown, string slug)
    {
        Match heading = Regex.Match(
            markdown,
            @"^\s*#\s+(?<title>.+?)\s*$",
            RegexOptions.Multiline);

        return heading.Success
            ? heading.Groups["title"].Value.Trim()
            : HumanizeSlug(slug);
    }

    private static string HumanizeSlug(string slug) =>
        slug.Replace('-', ' ').Replace('_', ' ').Trim();

    private static string BuildWikiPageUrl(string slug) =>
        $"https://github.com/{GondwanaMcpOptions.RepositoryFullName}/wiki/{EscapePath(slug)}";

    private static string EscapePath(string path) =>
        string.Join(
            '/',
            path.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
}
