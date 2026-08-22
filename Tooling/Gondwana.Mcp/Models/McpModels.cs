using System.Text.Json.Serialization;

namespace Gondwana.Mcp.Models;

public sealed record RepositoryInfoResult(
    string Repository,
    string ConfiguredDefaultRef,
    string GitHubDefaultBranch,
    string HeadSha,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTimeOffset? PushedAt,
    bool CodeSearchAuthenticated,
    string RepositoryUrl);

public sealed record RepositoryEntryResult(
    string Name,
    string Path,
    string Type,
    long Size,
    string Sha,
    string Url);

public sealed record DirectoryListingResult(
    string Repository,
    string Ref,
    string Path,
    IReadOnlyList<RepositoryEntryResult> Entries);

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

public sealed record CodeSearchMatchResult(
    string Path,
    string Sha,
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Fragment);

public sealed record CodeSearchResult(
    bool Available,
    string Repository,
    string BranchScope,
    string Query,
    int ReportedTotalCount,
    IReadOnlyList<CodeSearchMatchResult> Matches,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Message);

public sealed record WikiPageSummaryResult(
    string Title,
    string Slug,
    string Url);

public sealed record WikiPageListResult(
    int Count,
    IReadOnlyList<WikiPageSummaryResult> Pages);

public sealed record WikiPageResult(
    string Title,
    string Slug,
    string Url,
    string Markdown);

public sealed record WikiSearchMatchResult(
    string Title,
    string Slug,
    string Url,
    int Score,
    string Snippet);

public sealed record WikiSearchResult(
    string Query,
    int ScannedPages,
    IReadOnlyList<WikiSearchMatchResult> Matches);