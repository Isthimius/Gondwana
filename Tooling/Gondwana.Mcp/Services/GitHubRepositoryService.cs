using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gondwana.Mcp.Configuration;
using Gondwana.Mcp.Models;
using Microsoft.Extensions.Options;

namespace Gondwana.Mcp.Services;

public sealed class GitHubRepositoryService
{
    private const string GitHubApiVersion = "2022-11-28";

    private readonly HttpClient _httpClient;
    private readonly GondwanaMcpOptions _options;

    public GitHubRepositoryService(
        HttpClient httpClient,
        IOptions<GondwanaMcpOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress ??= new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Gondwana-MCP/1.0");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-GitHub-Api-Version",
            GitHubApiVersion);

        if (!string.IsNullOrWhiteSpace(_options.GitHubToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
        }
    }

    public bool HasAuthenticatedSearch =>
        !string.IsNullOrWhiteSpace(_options.GitHubToken);

    public async Task<RepositoryInfoResult> GetRepositoryInfoAsync(
        CancellationToken cancellationToken = default)
    {
        using JsonDocument repositoryDocument = await GetJsonAsync(
            $"repos/{GondwanaMcpOptions.RepositoryFullName}",
            cancellationToken);

        JsonElement repository = repositoryDocument.RootElement;

        string githubDefaultBranch =
            repository.GetProperty("default_branch").GetString()
            ?? GondwanaMcpOptions.DefaultRef;

        DateTimeOffset? pushedAt = null;
        if (repository.TryGetProperty("pushed_at", out JsonElement pushedElement) &&
            pushedElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(pushedElement.GetString(), out DateTimeOffset parsedPushedAt))
        {
            pushedAt = parsedPushedAt;
        }

        using JsonDocument commitDocument = await GetJsonAsync(
            $"repos/{GondwanaMcpOptions.RepositoryFullName}/commits/{Uri.EscapeDataString(githubDefaultBranch)}",
            cancellationToken);

        string headSha =
            commitDocument.RootElement.GetProperty("sha").GetString() ?? string.Empty;

        string repositoryUrl =
            repository.GetProperty("html_url").GetString()
            ?? $"https://github.com/{GondwanaMcpOptions.RepositoryFullName}";

        return new RepositoryInfoResult(
            GondwanaMcpOptions.RepositoryFullName,
            GondwanaMcpOptions.DefaultRef,
            githubDefaultBranch,
            headSha,
            pushedAt,
            HasAuthenticatedSearch,
            repositoryUrl);
    }

    public async Task<DirectoryListingResult> ListDirectoryAsync(
        string? path = null,
        string? @ref = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = NormalizeRepositoryPath(path);
        string normalizedRef = NormalizeRef(@ref);

        using JsonDocument document = await GetJsonAsync(
            BuildContentsUri(normalizedPath, normalizedRef),
            cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"'{normalizedPath}' is not a directory. Use read_repository_file for files.");
        }

        var entries = new List<RepositoryEntryResult>();

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            entries.Add(new RepositoryEntryResult(
                item.GetProperty("name").GetString() ?? string.Empty,
                item.GetProperty("path").GetString() ?? string.Empty,
                item.GetProperty("type").GetString() ?? string.Empty,
                item.TryGetProperty("size", out JsonElement size) ? size.GetInt64() : 0,
                item.GetProperty("sha").GetString() ?? string.Empty,
                item.GetProperty("html_url").GetString() ?? string.Empty));
        }

        return new DirectoryListingResult(
            GondwanaMcpOptions.RepositoryFullName,
            normalizedRef,
            normalizedPath,
            entries);
    }

    public async Task<FileReadResult> ReadFileAsync(
        string path,
        string? @ref = null,
        int startLine = 1,
        int? endLine = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = NormalizeRepositoryPath(path);

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new ArgumentException("A repository file path is required.", nameof(path));
        }

        if (startLine < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine), "startLine must be at least 1.");
        }

        if (endLine is not null && endLine < startLine)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endLine),
                "endLine cannot be less than startLine.");
        }

        string normalizedRef = NormalizeRef(@ref);

        using JsonDocument document = await GetJsonAsync(
            BuildContentsUri(normalizedPath, normalizedRef),
            cancellationToken);

        JsonElement root = document.RootElement;

        if (!string.Equals(root.GetProperty("type").GetString(), "file", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{normalizedPath}' is not a file. Use list_repository for directories.");
        }

        long reportedSize = root.TryGetProperty("size", out JsonElement size)
            ? size.GetInt64()
            : 0;

        if (reportedSize > _options.MaxFileBytes)
        {
            throw new InvalidOperationException(
                $"'{normalizedPath}' is {reportedSize:N0} bytes; the MCP read limit is {_options.MaxFileBytes:N0} bytes.");
        }

        string encoding = root.GetProperty("encoding").GetString() ?? string.Empty;
        if (!string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GitHub returned unsupported content encoding '{encoding}' for '{normalizedPath}'.");
        }

        string base64 = (root.GetProperty("content").GetString() ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        byte[] bytes = Convert.FromBase64String(base64);

        if (bytes.Length > _options.MaxFileBytes)
        {
            throw new InvalidOperationException(
                $"'{normalizedPath}' exceeds the MCP read limit of {_options.MaxFileBytes:N0} bytes.");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"'{normalizedPath}' is not UTF-8 text and cannot be returned by this source tool.",
                exception);
        }

        string normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        string[] lines = normalizedText.Split('\n');

        if (startLine > lines.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startLine),
                $"startLine {startLine} is beyond the file's {lines.Length} lines.");
        }

        int maximumEnd = Math.Min(
            lines.Length,
            startLine + _options.MaxLinesPerRead - 1);

        int requestedEnd = endLine ?? maximumEnd;
        int actualEnd = Math.Min(requestedEnd, maximumEnd);

        string content = string.Join(
            "\n",
            lines[(startLine - 1)..actualEnd]);

        bool hasMoreLines = actualEnd < lines.Length;

        return new FileReadResult(
            GondwanaMcpOptions.RepositoryFullName,
            normalizedRef,
            normalizedPath,
            root.GetProperty("sha").GetString() ?? string.Empty,
            root.GetProperty("html_url").GetString() ?? string.Empty,
            startLine,
            actualEnd,
            lines.Length,
            hasMoreLines,
            hasMoreLines ? actualEnd + 1 : null,
            content);
    }

    public async Task<CodeSearchResult> SearchCodeAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = (query ?? string.Empty).Trim();

        if (normalizedQuery.Length == 0)
        {
            throw new ArgumentException("A code-search query is required.", nameof(query));
        }

        if (normalizedQuery.Length > 250)
        {
            throw new ArgumentException(
                "Code-search queries are limited to 250 characters.",
                nameof(query));
        }

        int resultLimit = Math.Clamp(maxResults, 1, _options.MaxSearchResults);

        if (!HasAuthenticatedSearch)
        {
            return new CodeSearchResult(
                false,
                GondwanaMcpOptions.RepositoryFullName,
                "GitHub repository default branch",
                normalizedQuery,
                0,
                [],
                "Server-side GitHub authentication is not configured. " +
                "Set GondwanaMcp__GitHubToken to enable GitHub code search. " +
                "The token remains on the server and is never exposed to MCP clients.");
        }

        string githubQuery =
            $"{normalizedQuery} repo:{GondwanaMcpOptions.RepositoryFullName}";

        string uri =
            $"search/code?q={Uri.EscapeDataString(githubQuery)}&per_page={resultLimit}&page=1";

        using JsonDocument document = await GetJsonAsync(
            uri,
            cancellationToken,
            "application/vnd.github.text-match+json");

        JsonElement root = document.RootElement;

        int reportedTotal =
            root.TryGetProperty("total_count", out JsonElement total)
                ? total.GetInt32()
                : 0;

        var matches = new List<CodeSearchMatchResult>();

        if (root.TryGetProperty("items", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                string resultRepository = item
                    .GetProperty("repository")
                    .GetProperty("full_name")
                    .GetString() ?? string.Empty;

                // Defense in depth: crafted query qualifiers cannot make this service
                // return a result from any repository other than Gondwana.
                if (!string.Equals(
                        resultRepository,
                        GondwanaMcpOptions.RepositoryFullName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? fragment = null;

                if (item.TryGetProperty("text_matches", out JsonElement textMatches) &&
                    textMatches.ValueKind == JsonValueKind.Array)
                {
                    string joined = string.Join(
                        "\n...\n",
                        textMatches
                            .EnumerateArray()
                            .Select(match =>
                                match.TryGetProperty("fragment", out JsonElement value)
                                    ? value.GetString()
                                    : null)
                            .Where(value => !string.IsNullOrWhiteSpace(value)));

                    if (!string.IsNullOrWhiteSpace(joined))
                    {
                        fragment = joined.Length > 1_500
                            ? joined[..1_500] + "…"
                            : joined;
                    }
                }

                matches.Add(new CodeSearchMatchResult(
                    item.GetProperty("path").GetString() ?? string.Empty,
                    item.GetProperty("sha").GetString() ?? string.Empty,
                    item.GetProperty("html_url").GetString() ?? string.Empty,
                    fragment));
            }
        }

        return new CodeSearchResult(
            true,
            GondwanaMcpOptions.RepositoryFullName,
            "GitHub repository default branch",
            normalizedQuery,
            reportedTotal,
            matches,
            null);
    }

    private async Task<JsonDocument> GetJsonAsync(
        string relativeUri,
        CancellationToken cancellationToken,
        string accept = "application/vnd.github+json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowGitHubErrorAsync(response, cancellationToken);
        }

        await using Stream stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
    }

    private static async Task ThrowGitHubErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string message = response.ReasonPhrase ?? "GitHub request failed.";

        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument error = JsonDocument.Parse(body);

            if (error.RootElement.TryGetProperty("message", out JsonElement value) &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                message = value.GetString()!;
            }
        }
        catch
        {
            // Preserve the HTTP status/reason if the error response is not JSON.
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException(
                $"GitHub returned 404 for the configured Gondwana repository: {message}");
        }

        throw new HttpRequestException(
            $"GitHub request failed with {(int)response.StatusCode} {response.StatusCode}: {message}",
            null,
            response.StatusCode);
    }

    private static string BuildContentsUri(string path, string @ref)
    {
        string escapedPath = EscapePath(path);
        string suffix = escapedPath.Length == 0 ? string.Empty : "/" + escapedPath;

        return
            $"repos/{GondwanaMcpOptions.RepositoryFullName}/contents{suffix}?ref={Uri.EscapeDataString(@ref)}";
    }

    private static string NormalizeRepositoryPath(string? path)
    {
        string normalized = (path ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .Trim('/');

        if (normalized.Length > 500)
        {
            throw new ArgumentException(
                "Repository paths are limited to 500 characters.",
                nameof(path));
        }

        string[] segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                "Repository paths cannot contain '.' or '..' traversal segments.",
                nameof(path));
        }

        return string.Join('/', segments);
    }

    private static string NormalizeRef(string? @ref)
    {
        string normalized = string.IsNullOrWhiteSpace(@ref)
            ? GondwanaMcpOptions.DefaultRef
            : @ref.Trim();

        if (normalized.Length > 200 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The repository ref is invalid.", nameof(@ref));
        }

        return normalized;
    }

    private static string EscapePath(string path) =>
        string.Join(
            '/',
            path.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
}
