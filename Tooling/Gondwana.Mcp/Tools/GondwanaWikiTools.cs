using System.ComponentModel;
using Gondwana.Mcp.Models;
using Gondwana.Mcp.Services;
using ModelContextProtocol.Server;

namespace Gondwana.Mcp.Tools;

[McpServerToolType]
public sealed class GondwanaWikiTools
{
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
