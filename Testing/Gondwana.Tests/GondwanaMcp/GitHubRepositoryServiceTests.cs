using System.Text;
using System.Text.Json;
using Gondwana.Mcp.Configuration;
using Gondwana.Mcp.Services;
using Microsoft.Extensions.Options;

namespace Gondwana.Tests.GondwanaMcp;

public sealed class GitHubRepositoryServiceTests
{
    [Fact]
    public async Task ReadFile_ReturnsBoundedLineRangeAndContinuation()
    {
        const string source = "one\ntwo\nthree\nfour\nfive";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(source));

        string responseJson = JsonSerializer.Serialize(new
        {
            type = "file",
            size = Encoding.UTF8.GetByteCount(source),
            encoding = "base64",
            content = encoded,
            sha = "abc123",
            html_url = "https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Engine.cs"
        });

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(_ =>
                TestHttpMessageHandler.Json(responseJson)));

        var service = new GitHubRepositoryService(
            httpClient,
            Options.Create(new GondwanaMcpOptions
            {
                MaxLinesPerRead = 2
            }));

        var result = await service.ReadFileAsync(
            "Gondwana/Engine.cs",
            "master",
            startLine: 2);

        Assert.Equal("Isthimius/Gondwana", result.Repository);
        Assert.Equal("master", result.Ref);
        Assert.Equal(2, result.StartLine);
        Assert.Equal(3, result.EndLine);
        Assert.Equal(5, result.TotalLines);
        Assert.True(result.HasMoreLines);
        Assert.Equal(4, result.NextStartLine);
        Assert.Equal("two\nthree", result.Content);
    }

    [Fact]
    public async Task ListDirectory_RejectsTraversalBeforeCallingGitHub()
    {
        bool called = false;

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(_ =>
            {
                called = true;
                return TestHttpMessageHandler.Json("[]");
            }));

        var service = new GitHubRepositoryService(
            httpClient,
            Options.Create(new GondwanaMcpOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListDirectoryAsync("../private"));

        Assert.False(called);
    }

    [Fact]
    public async Task SearchCode_FiltersUnexpectedRepositoryFromGitHubResponse()
    {
        string responseJson = JsonSerializer.Serialize(new
        {
            total_count = 2,
            items = new object[]
            {
                new
                {
                    path = "Gondwana/Engine.cs",
                    sha = "gondwana-sha",
                    html_url = "https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Engine.cs",
                    repository = new { full_name = "Isthimius/Gondwana" },
                    text_matches = new[]
                    {
                        new { fragment = "public static class Engine" }
                    }
                },
                new
                {
                    path = "Elsewhere/Engine.cs",
                    sha = "other-sha",
                    html_url = "https://github.com/example/other/blob/master/Elsewhere/Engine.cs",
                    repository = new { full_name = "example/other" },
                    text_matches = Array.Empty<object>()
                }
            }
        });

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(request =>
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("server-token", request.Headers.Authorization?.Parameter);
                return TestHttpMessageHandler.Json(responseJson);
            }));

        var service = new GitHubRepositoryService(
            httpClient,
            Options.Create(new GondwanaMcpOptions
            {
                GitHubToken = "server-token"
            }));

        var result = await service.SearchCodeAsync("Engine");

        Assert.True(result.Available);
        Assert.Single(result.Matches);
        Assert.Equal("Gondwana/Engine.cs", result.Matches[0].Path);
    }

    [Fact]
    public async Task SearchCode_WithoutServerTokenDoesNotCallGitHub()
    {
        bool called = false;

        using var httpClient = new HttpClient(
            new TestHttpMessageHandler(_ =>
            {
                called = true;
                return TestHttpMessageHandler.Json("{}");
            }));

        var service = new GitHubRepositoryService(
            httpClient,
            Options.Create(new GondwanaMcpOptions()));

        var result = await service.SearchCodeAsync("SceneLayer");

        Assert.False(result.Available);
        Assert.Empty(result.Matches);
        Assert.False(called);
    }
}
