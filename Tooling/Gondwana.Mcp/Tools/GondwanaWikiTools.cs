using System.ComponentModel;
using Gondwana.Mcp.Models;
using Gondwana.Mcp.Services;
using ModelContextProtocol.Server;

namespace Gondwana.Mcp.Tools;

/// <summary>
/// Represents gondwana wiki tools.
/// </summary>
[McpServerToolType]
public sealed class GondwanaWikiTools
{
    /// <summary>
    /// Lists the available Gondwana wiki pages.
    /// </summary>
    /// <param name="wiki">The wiki.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task whose result contains the available wiki pages.</returns>
    [McpServerTool(
        Name = "list_wiki_pages",
        Title = "List Gondwana wiki pages",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Lists pages in the official Gondwana GitHub wiki. " +
        "Use this to discover human-facing documentation for an engine topic.")]
    public static Task<WikiPageListResult> ListWikiPagesAsync(
        GondwanaWikiService wiki,
        CancellationToken cancellationToken = default) =>
        wiki.ListPagesAsync(cancellationToken);

    /// <summary>
    /// Reads the Markdown content of a Gondwana wiki page.
    /// </summary>
    /// <param name="wiki">The wiki.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task whose result contains the wiki page title, URL, and Markdown.</returns>
    [McpServerTool(
        Name = "read_wiki_page",
        Title = "Read Gondwana wiki page",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Reads Markdown from one page in the official Gondwana GitHub wiki only. " +
        "Accepts a title/slug or wiki URL; URLs are reduced to a Gondwana page slug.")]
    public static Task<WikiPageResult> ReadWikiPageAsync(
        GondwanaWikiService wiki,
        [Description("Gondwana wiki page title, slug, or URL.")]
        string page,
        CancellationToken cancellationToken = default) =>
        wiki.ReadPageAsync(page, cancellationToken);

    /// <summary>
    /// Searches Gondwana wiki titles and content.
    /// </summary>
    /// <param name="wiki">The wiki.</param>
    /// <param name="query">The search text.</param>
    /// <param name="maxResults">The maximum number of matching results to return.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task whose result contains matching wiki pages and excerpts.</returns>
    [McpServerTool(
        Name = "search_wiki",
        Title = "Search Gondwana wiki",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Searches titles and Markdown content across the official Gondwana GitHub wiki only. " +
        "Use this for architecture, terminology, intended usage, and subsystem documentation.")]
    public static Task<WikiSearchResult> SearchWikiAsync(
        GondwanaWikiService wiki,
        [Description("Documentation text or concept to search for.")]
        string query,
        [Description("Maximum matches to return.")]
        int maxResults = 10,
        CancellationToken cancellationToken = default) =>
        wiki.SearchAsync(query, maxResults, cancellationToken);
}
