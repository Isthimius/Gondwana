using Gondwana.Mcp.Configuration;
using Gondwana.Mcp.Services;
using Gondwana.Mcp.Tools;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<GondwanaMcpOptions>()
    .Bind(builder.Configuration.GetSection(GondwanaMcpOptions.SectionName))
    .Validate(
        options => options.MaxFileBytes is >= 16_384 and <= 2_000_000,
        $"{nameof(GondwanaMcpOptions.MaxFileBytes)} must be between 16 KB and 2 MB.")
    .Validate(
        options => options.MaxLinesPerRead is >= 25 and <= 2_000,
        $"{nameof(GondwanaMcpOptions.MaxLinesPerRead)} must be between 25 and 2,000.")
    .Validate(
        options => options.MaxSearchResults is >= 1 and <= 50,
        $"{nameof(GondwanaMcpOptions.MaxSearchResults)} must be between 1 and 50.")
    .Validate(
        options => options.WikiCacheMinutes is >= 1 and <= 1_440,
        $"{nameof(GondwanaMcpOptions.WikiCacheMinutes)} must be between 1 minute and 24 hours.")
    .Validate(
        options => options.WikiSearchConcurrency is >= 1 and <= 16,
        $"{nameof(GondwanaMcpOptions.WikiSearchConcurrency)} must be between 1 and 16.")
    .ValidateOnStart();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<GitHubRepositoryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<GondwanaWikiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<GondwanaRepositoryTools>()
    .WithTools<GondwanaWikiTools>();

var app = builder.Build();

app.MapGet("/", (GitHubRepositoryService repository) => Results.Ok(new
{
    name = "Gondwana MCP",
    repository = GondwanaMcpOptions.RepositoryFullName,
    defaultRef = GondwanaMcpOptions.DefaultRef,
    access = "read-only",
    codeSearchAuthenticated = repository.HasAuthenticatedSearch,
    mcpEndpoint = "/mcp",
    healthEndpoint = "/health"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    repository = GondwanaMcpOptions.RepositoryFullName,
    access = "read-only"
}));

app.MapMcp("/mcp");

app.Run();

public partial class Program
{
}
