# Gondwana MCP

`Gondwana.Mcp` is the read-only Model Context Protocol (MCP) server for the Gondwana Game Engine.

It gives MCP-capable AI clients a deliberately narrow set of tools for inspecting the current public Gondwana repository and its GitHub wiki without granting those clients write access to GitHub.

## Scope

The server is hard-wired to:

- repository: `Isthimius/Gondwana`
- public source ref when none is supplied: `master`
- documentation: the official Gondwana GitHub wiki
- access model: read-only

The repository owner/name are compile-time constants rather than tool parameters or deployment configuration. MCP callers therefore cannot repurpose this server to browse another repository.

## MCP endpoint

The included development profile uses:

```text
http://localhost:3001/mcp
```

Health/info endpoints:

```text
GET http://localhost:3001/
GET http://localhost:3001/health
```

The MCP transport is stateless Streamable HTTP.

## Tools

Repository tools:

- `get_repository_info`
- `list_repository`
- `read_repository_file`
- `search_repository`

Wiki tools:

- `list_wiki_pages`
- `read_wiki_page`
- `search_wiki`

Every MCP tool is annotated as read-only and idempotent.

## GitHub authentication

Public repository list/read operations work without a GitHub token.

GitHub code search should use a server-side token. The MCP client never needs access to the user's GitHub account.

```powershell
$env:GondwanaMcp__GitHubToken = "github_pat_..."
dotnet run --project Tooling/Gondwana.Mcp
```

Use a narrowly scoped read-only token. The server never returns the token in tool results.

Without a token, `search_repository` returns a structured "unavailable" result while repository list/read and wiki tools continue to work.

## Configuration

Non-secret limits live in `appsettings.json`:

```json
{
  "GondwanaMcp": {
    "MaxFileBytes": 524288,
    "MaxLinesPerRead": 400,
    "MaxSearchResults": 20,
    "WikiCacheMinutes": 15,
    "WikiSearchConcurrency": 6
  }
}
```

Secrets should come from environment variables, user secrets, or the deployment platform's secret store.

For production, set `AllowedHosts` to the exact public host name instead of using a wildcard.

## Docker

Build from the repository root:

```console
docker build -f Tooling/Gondwana.Mcp/Dockerfile -t gondwana-mcp .
```

Run locally:

```console
docker run --rm -p 8080:8080 ^
  -e AllowedHosts=localhost ^
  -e GondwanaMcp__GitHubToken=github_pat_... ^
  gondwana-mcp
```

The container exposes:

```text
http://localhost:8080/mcp
```

A public deployment should terminate TLS and expose the MCP endpoint over HTTPS.

## Design notes

The service exposes no generic URL-fetch, arbitrary-repository, Git write, issue-write, branch-write, or pull-request tools.

Source-sensitive questions should normally begin with `get_repository_info` and then inspect current source/tests. The wiki remains the explanation of intended architecture and usage; source and tests remain authoritative for current implementation.

This project is the repository-access layer intended for later packaging as the official Gondwana AI plugin/app.
