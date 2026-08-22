namespace Gondwana.Mcp.Configuration;

public sealed class GondwanaMcpOptions
{
    public const string SectionName = "GondwanaMcp";

    // Deliberately compile-time scoped. MCP callers and deployment settings cannot
    // redirect this service to another repository.
    public const string RepositoryOwner = "Isthimius";
    public const string RepositoryName = "Gondwana";
    public const string RepositoryFullName = RepositoryOwner + "/" + RepositoryName;
    public const string DefaultRef = "master";

    public string? GitHubToken { get; set; }

    public int MaxFileBytes { get; set; } = 524_288;

    public int MaxLinesPerRead { get; set; } = 400;

    public int MaxSearchResults { get; set; } = 20;

    public int WikiCacheMinutes { get; set; } = 15;

    public int WikiSearchConcurrency { get; set; } = 6;
}
