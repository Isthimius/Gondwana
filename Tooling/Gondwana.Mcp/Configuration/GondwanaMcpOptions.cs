namespace Gondwana.Mcp.Configuration;

/// <summary>
/// Represents gondwana mcp options.
/// </summary>
public sealed class GondwanaMcpOptions
{
    /// <summary>
    /// The section name.
    /// </summary>
    public const string SectionName = "GondwanaMcp";

    // Deliberately compile-time scoped. MCP callers and deployment settings cannot
    // redirect this service to another repository.
    /// <summary>
    /// The repository owner.
    /// </summary>
    public const string RepositoryOwner = "Isthimius";
    /// <summary>
    /// The repository name.
    /// </summary>
    public const string RepositoryName = "Gondwana";
    /// <summary>
    /// The repository full name.
    /// </summary>
    public const string RepositoryFullName = RepositoryOwner + "/" + RepositoryName;
    /// <summary>
    /// The default ref.
    /// </summary>
    public const string DefaultRef = "master";

    /// <summary>
    /// Gets or sets the git hub token.
    /// </summary>
    public string? GitHubToken { get; set; }

    // Set only when the OpenAI submission portal issues a domain-verification token.
    /// <summary>
    /// Gets or sets the open ai apps challenge token.
    /// </summary>
    public string? OpenAiAppsChallengeToken { get; set; }

    /// <summary>
    /// Gets or sets the max file bytes.
    /// </summary>
    public int MaxFileBytes { get; set; } = 524_288;

    /// <summary>
    /// Gets or sets the max lines per read.
    /// </summary>
    public int MaxLinesPerRead { get; set; } = 400;

    /// <summary>
    /// Gets or sets the max search results.
    /// </summary>
    public int MaxSearchResults { get; set; } = 20;

    /// <summary>
    /// Gets or sets the wiki cache minutes.
    /// </summary>
    public int WikiCacheMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the wiki search concurrency.
    /// </summary>
    public int WikiSearchConcurrency { get; set; } = 6;
}
