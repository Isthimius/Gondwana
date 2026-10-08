using System.Text.Json.Serialization;

namespace Gondwana.Mcp.Models;

/// <summary>
/// Represents repository info result.
/// </summary>
/// <param name="Repository">The repository identity.</param>
/// <param name="ConfiguredDefaultRef">The configured default ref.</param>
/// <param name="GitHubDefaultBranch">The git hub default branch.</param>
/// <param name="HeadSha">The head sha.</param>
/// <param name="PushedAt">The pushed at.</param>
/// <param name="CodeSearchAuthenticated">Whether authenticated repository code search is available.</param>
/// <param name="RepositoryUrl">The repository url.</param>
public sealed record RepositoryInfoResult(
    string Repository,
    string ConfiguredDefaultRef,
    string GitHubDefaultBranch,
    string HeadSha,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTimeOffset? PushedAt,
    bool CodeSearchAuthenticated,
    string RepositoryUrl);

/// <summary>
/// Represents repository entry result.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Path">The path.</param>
/// <param name="Type">The type.</param>
/// <param name="Size">The size.</param>
/// <param name="Sha">The sha.</param>
/// <param name="Url">The canonical URL of the resource.</param>
public sealed record RepositoryEntryResult(
    string Name,
    string Path,
    string Type,
    long Size,
    string Sha,
    string Url);

/// <summary>
/// Represents directory listing result.
/// </summary>
/// <param name="Repository">The repository identity.</param>
/// <param name="Ref">The repository branch, tag, or commit to read.</param>
/// <param name="Path">The path.</param>
/// <param name="Entries">The entries.</param>
public sealed record DirectoryListingResult(
    string Repository,
    string Ref,
    string Path,
    IReadOnlyList<RepositoryEntryResult> Entries);

/// <summary>
/// Represents file read result.
/// </summary>
/// <param name="Repository">The repository identity.</param>
/// <param name="Ref">The repository branch, tag, or commit to read.</param>
/// <param name="Path">The path.</param>
/// <param name="Sha">The sha.</param>
/// <param name="Url">The canonical URL of the resource.</param>
/// <param name="StartLine">The first line to read, using one-based numbering.</param>
/// <param name="EndLine">The end line.</param>
/// <param name="TotalLines">The total lines.</param>
/// <param name="HasMoreLines">Whether more file lines follow the returned excerpt.</param>
/// <param name="NextStartLine">The next start line.</param>
/// <param name="Content">The content.</param>
public sealed record FileReadResult(
    string Repository,
    string Ref,
    string Path,
    string Sha,
    string Url,
    int StartLine,
    int EndLine,
    int TotalLines,
    bool HasMoreLines,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    int? NextStartLine,
    string Content);

/// <summary>
/// Represents code search match result.
/// </summary>
/// <param name="Path">The path.</param>
/// <param name="Sha">The sha.</param>
/// <param name="Url">The canonical URL of the resource.</param>
/// <param name="Fragment">The fragment.</param>
public sealed record CodeSearchMatchResult(
    string Path,
    string Sha,
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Fragment);

/// <summary>
/// Represents code search result.
/// </summary>
/// <param name="Available">Whether the requested search capability is available.</param>
/// <param name="Repository">The repository identity.</param>
/// <param name="BranchScope">The branch scope.</param>
/// <param name="Query">The search text.</param>
/// <param name="ReportedTotalCount">The reported total count.</param>
/// <param name="Matches">The matches.</param>
/// <param name="Message">The message.</param>
public sealed record CodeSearchResult(
    bool Available,
    string Repository,
    string BranchScope,
    string Query,
    int ReportedTotalCount,
    IReadOnlyList<CodeSearchMatchResult> Matches,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Message);

/// <summary>
/// Represents wiki page summary result.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Slug">The wiki page identifier.</param>
/// <param name="Url">The canonical URL of the resource.</param>
public sealed record WikiPageSummaryResult(
    string Title,
    string Slug,
    string Url);

/// <summary>
/// Represents wiki page list result.
/// </summary>
/// <param name="Count">The count.</param>
/// <param name="Pages">The pages.</param>
public sealed record WikiPageListResult(
    int Count,
    IReadOnlyList<WikiPageSummaryResult> Pages);

/// <summary>
/// Represents wiki page result.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Slug">The wiki page identifier.</param>
/// <param name="Url">The canonical URL of the resource.</param>
/// <param name="Markdown">The markdown.</param>
public sealed record WikiPageResult(
    string Title,
    string Slug,
    string Url,
    string Markdown);

/// <summary>
/// Represents wiki search match result.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Slug">The wiki page identifier.</param>
/// <param name="Url">The canonical URL of the resource.</param>
/// <param name="Score">The score.</param>
/// <param name="Snippet">The snippet.</param>
public sealed record WikiSearchMatchResult(
    string Title,
    string Slug,
    string Url,
    int Score,
    string Snippet);

/// <summary>
/// Represents wiki search result.
/// </summary>
/// <param name="Query">The search text.</param>
/// <param name="ScannedPages">The scanned pages.</param>
/// <param name="Matches">The matches.</param>
public sealed record WikiSearchResult(
    string Query,
    int ScannedPages,
    IReadOnlyList<WikiSearchMatchResult> Matches);
