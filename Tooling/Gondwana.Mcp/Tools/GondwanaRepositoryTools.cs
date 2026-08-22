using System.ComponentModel;
using Gondwana.Mcp.Models;
using Gondwana.Mcp.Services;
using ModelContextProtocol.Server;

namespace Gondwana.Mcp.Tools;

[McpServerToolType]
public sealed class GondwanaRepositoryTools
{
    [McpServerTool(
        Name = "get_repository_info",
        Title = "Get Gondwana repository info",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Returns identity and current-head information for the official Isthimius/Gondwana repository. " +
        "Use this before source-sensitive work to establish which public branch/commit is current.")]
    public static Task<RepositoryInfoResult> GetRepositoryInfoAsync(
        GitHubRepositoryService repository,
        CancellationToken cancellationToken = default) =>
        repository.GetRepositoryInfoAsync(cancellationToken);

    [McpServerTool(
        Name = "list_repository",
        Title = "List Gondwana repository path",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Lists files and directories inside the official Isthimius/Gondwana repository only. " +
        "Defaults to master. Use it to navigate before reading source.")]
    public static Task<DirectoryListingResult> ListRepositoryAsync(
        GitHubRepositoryService repository,
        [Description("Repository-relative directory path. Empty means repository root.")]
        string? path = null,
        [Description("Git branch, tag, or commit SHA. Defaults to master.")]
        string? @ref = null,
        CancellationToken cancellationToken = default) =>
        repository.ListDirectoryAsync(path, @ref, cancellationToken);

    [McpServerTool(
        Name = "read_repository_file",
        Title = "Read Gondwana repository file",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Reads a bounded line range from a UTF-8 text file in the official Isthimius/Gondwana repository only. " +
        "Defaults to master and returns continuation information when more lines remain.")]
    public static Task<FileReadResult> ReadRepositoryFileAsync(
        GitHubRepositoryService repository,
        [Description("Repository-relative file path, for example Gondwana/Engine.cs.")]
        string path,
        [Description("Git branch, tag, or commit SHA. Defaults to master.")]
        string? @ref = null,
        [Description("1-based first line to return.")]
        int startLine = 1,
        [Description("Optional 1-based final line. The server still enforces its per-read line limit.")]
        int? endLine = null,
        CancellationToken cancellationToken = default) =>
        repository.ReadFileAsync(
            path,
            @ref,
            startLine,
            endLine,
            cancellationToken);

    [McpServerTool(
        Name = "search_repository",
        Title = "Search Gondwana source",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Searches code in the official Isthimius/Gondwana repository only using GitHub code search. " +
        "This searches the repository's GitHub default branch and requires a server-side read-only GitHub token.")]
    public static Task<CodeSearchResult> SearchRepositoryAsync(
        GitHubRepositoryService repository,
        [Description("Code or symbol text to search for.")]
        string query,
        [Description("Maximum matches to return.")]
        int maxResults = 10,
        CancellationToken cancellationToken = default) =>
        repository.SearchCodeAsync(query, maxResults, cancellationToken);
}
