> **Audience:** Gondwana engine maintainers, including “me six months from now.”
>
> **Purpose:** Record how the public Gondwana Model Context Protocol (MCP) service is structured, hosted, connected to the repository/plugin, deployed, and verified so it can be maintained or recreated without reconstructing the setup from memory.
>
> **Current production endpoint:** `https://mcp.hiddenworldsgames.com/mcp`
>
> **Current host:** Render.com
>
> **Repository:** `Isthimius/Gondwana`
>
> **Production branch:** `master`

---

## 1. What the MCP Service Is

`Gondwana.Mcp` is a small ASP.NET Core/.NET 8 web service that exposes **read-only Model Context Protocol tools** for the public Gondwana repository and official Gondwana GitHub wiki.

Its purpose is to give ChatGPT, Codex, and other MCP-capable clients access to current Gondwana source and documentation without giving those clients GitHub write access.

The service is intentionally hard-scoped to:

- Repository: `Isthimius/Gondwana`
- Default source ref: `master`
- Documentation: the official Gondwana GitHub wiki
- Access model: read-only
- End-user authentication: none
- Arbitrary external URLs/repositories: not supported

The service itself does **not** contain a baked-in copy of Gondwana source or documentation. Repository reads are made live against GitHub. Wiki content is fetched from the official Gondwana wiki and cached briefly in memory.

This distinction matters for deployment:

- A source/doc change merged to `master` becomes readable by the existing MCP service without requiring a new MCP build.
- A change to the actual `Tooling/Gondwana.Mcp` implementation requires the hosted service to rebuild/redeploy.

With the **current Render configuration**, every commit to `master` triggers a Render deployment anyway because no build filters are configured. See [Render auto-deploy behavior](#10-render-auto-deploy-behavior).

---

## 2. Repository History

The initial MCP infrastructure was introduced in these pull requests:

### PR #274 — Initial MCP service and AI plugin

**Merged:** August 22, 2026  
**PR:** https://github.com/Isthimius/Gondwana/pull/274

This added:

- `Tooling/Gondwana.Mcp`
- Repository read/list/search tools
- Wiki read/list/search tools
- Docker deployment support
- MCP unit tests
- `docs/ai/` repository-awareness documentation
- Gondwana AI/plugin metadata and skills
- Initial Render-oriented hosting support

### PR #276 — MCP/plugin audit and compliance

**Merged:** August 22, 2026  
**PR:** https://github.com/Isthimius/Gondwana/pull/276

This added/refined:

- Tool metadata and MCP safety annotations
- OpenAI/plugin compliance documentation
- Privacy, terms, support, and submission documentation
- MCP tool metadata tests
- Domain-verification support
- Docker and production configuration cleanup

### PR #287 — Production hostname migration

**Merged:** August 26, 2026  
**PR:** https://github.com/Isthimius/Gondwana/pull/287

This changed the public MCP hostname to:

```text
https://mcp.hiddenworldsgames.com/mcp
```

and updated the plugin configuration, documentation, and skills accordingly.

---

## 3. Important Repository Files

The MCP implementation lives under:

```text
Tooling/Gondwana.Mcp/
```

### Core project files

| File | Purpose |
|---|---|
| `Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj` | ASP.NET Core `.NET 8` web project; references `ModelContextProtocol.AspNetCore`. |
| `Tooling/Gondwana.Mcp/Program.cs` | Configures dependency injection, HTTP clients, MCP server, tools, `/health`, `/`, challenge endpoint, and `/mcp`. |
| `Tooling/Gondwana.Mcp/Dockerfile` | Production container build used by Render. |
| `Tooling/Gondwana.Mcp/appsettings.json` | Non-secret service limits and local/default host settings. |
| `Tooling/Gondwana.Mcp/Properties/launchSettings.json` | Local development profile on `http://localhost:3001`. |
| `Tooling/Gondwana.Mcp/README.md` | Primary maintainer/developer README for the MCP service. |

Repository links:

- https://github.com/Isthimius/Gondwana/tree/master/Tooling/Gondwana.Mcp
- https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/README.md
- https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Program.cs
- https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Dockerfile

### Configuration

`Configuration/GondwanaMcpOptions.cs` hard-codes the repository boundary:

```csharp
public const string RepositoryOwner = "Isthimius";
public const string RepositoryName = "Gondwana";
public const string RepositoryFullName = RepositoryOwner + "/" + RepositoryName;
public const string DefaultRef = "master";
```

This is deliberate. MCP callers and deployment configuration cannot redirect the server to an arbitrary GitHub repository.

File:

https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Configuration/GondwanaMcpOptions.cs

### Repository service

`Services/GitHubRepositoryService.cs` performs live GitHub API requests for repository information, listings, file reads, and code search.

Important behavior:

- Repository list/read defaults to `master`.
- Source files are **not cached by the MCP process**.
- A merge to `master` is therefore immediately visible to subsequent repository reads.
- GitHub code search requires a server-side GitHub token.
- The server verifies that returned search matches belong to `Isthimius/Gondwana`.
- File size and line count limits are enforced before content is returned.

File:

https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Services/GitHubRepositoryService.cs

### Wiki service

`Services/GondwanaWikiService.cs` handles:

- Wiki page discovery
- Markdown retrieval
- Wiki search
- In-memory wiki caching

Unlike repository source reads, wiki content has an in-memory cache. The default cache duration is currently 15 minutes.

File:

https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Services/GondwanaWikiService.cs

### MCP tool declarations

Repository tools are in:

```text
Tooling/Gondwana.Mcp/Tools/GondwanaRepositoryTools.cs
```

The four repository tools are:

```text
get_repository_info
list_repository
read_repository_file
search_repository
```

Wiki tools are in:

```text
Tooling/Gondwana.Mcp/Tools/GondwanaWikiTools.cs
```

The three wiki tools are:

```text
list_wiki_pages
read_wiki_page
search_wiki
```

All seven are explicitly annotated as:

```text
ReadOnly   = true
Destructive = false
Idempotent = true
OpenWorld  = false
```

Files:

- https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Tools/GondwanaRepositoryTools.cs
- https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Tools/GondwanaWikiTools.cs

### Result models

Structured MCP return types are defined in:

```text
Tooling/Gondwana.Mcp/Models/McpModels.cs
```

File:

https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/Models/McpModels.cs

---

## 4. Service Endpoints

`Program.cs` exposes the following endpoints.

### MCP endpoint

```text
https://mcp.hiddenworldsgames.com/mcp
```

This is the production MCP Streamable HTTP endpoint.

### Health endpoint

```text
https://mcp.hiddenworldsgames.com/health
```

Expected shape:

```json
{
  "status": "ok",
  "repository": "Isthimius/Gondwana",
  "access": "read-only"
}
```

Render is configured to use:

```text
/health
```

as the service health-check path.

### Service information endpoint

```text
https://mcp.hiddenworldsgames.com/
```

This reports basic service information including repository identity, default ref, MCP endpoint, health endpoint, and whether authenticated GitHub code search is available.

### OpenAI domain-verification endpoint

```text
https://mcp.hiddenworldsgames.com/.well-known/openai-apps-challenge
```

This returns the exact OpenAI challenge token when the corresponding environment variable is configured.

If no token is configured, the endpoint returns `404`.

---

## 5. Local Development

### Run directly with .NET

From the repository root:

```powershell
dotnet run --project Tooling/Gondwana.Mcp
```

The development launch profile uses:

```text
http://localhost:3001
```

Useful local URLs:

```text
http://localhost:3001/
http://localhost:3001/health
http://localhost:3001/mcp
```

### Enable authenticated GitHub code search locally

Set a narrowly scoped server-side token:

```powershell
$env:GondwanaMcp__GitHubToken = "github_pat_..."
dotnet run --project Tooling/Gondwana.Mcp
```

Without this token:

- repository information works
- directory listing works
- repository file reads work
- wiki tools work
- `search_repository` reports that authenticated search is unavailable

The token must never be returned to MCP clients or committed to source control.

---

## 6. Docker Build

The production service uses:

```text
Tooling/Gondwana.Mcp/Dockerfile
```

The Dockerfile is a two-stage .NET 8 build.

### Build stage

```text
mcr.microsoft.com/dotnet/sdk:8.0
```

It copies these root build files first:

```text
Directory.Build.props
Directory.Build.targets
global.json
Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj
```

It then restores the MCP project, copies the repository, and runs:

```text
dotnet publish Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj
    --configuration Release
    --no-restore
    --output /app/publish
```

### Runtime stage

The runtime image is:

```text
mcr.microsoft.com/dotnet/aspnet:8.0
```

The container configures:

```text
ASPNETCORE_URLS=http://+:8080
```

and starts:

```text
dotnet Gondwana.Mcp.dll
```

### Build locally

Run this from the **repository root**:

```powershell
docker build -f Tooling/Gondwana.Mcp/Dockerfile -t gondwana-mcp .
```

The final `.` matters because the build context must include the repository root.

### Run locally in Docker

Example:

```powershell
docker run --rm -p 8080:8080 `
  -e AllowedHosts=localhost `
  -e GondwanaMcp__GitHubToken=github_pat_... `
  gondwana-mcp
```

Then test:

```text
http://localhost:8080/
http://localhost:8080/health
http://localhost:8080/mcp
```

---

## 7. Why the Render Build Context Is the Repository Root

The production Render settings currently use:

```text
Root Directory:                 (blank)
Dockerfile Path:                Tooling/Gondwana.Mcp/Dockerfile
Docker Build Context Directory: .
```

This is correct for the current Dockerfile.

Do **not** casually set the Render Root Directory to `Tooling/Gondwana.Mcp`.

The Dockerfile intentionally accesses repository-root files:

```text
Directory.Build.props
Directory.Build.targets
global.json
```

and performs:

```dockerfile
COPY . .
```

Therefore the Docker build context needs to remain the repository root unless the Dockerfile/build structure is redesigned.

---

## 8. Current Render.com Service Configuration

As of September 30, 2026, the production service is configured as follows.

### General

```text
Service name:  gondwana-mcp
Service type:  Web Service
Runtime:       Docker
Plan:          Starter
Region:        Virginia (US East)
Service ID:    srv-da4uiarbc2fs73fa28rg
Repository:    Isthimius / Gondwana
Branch:        master
```

Production hostname:

```text
mcp.hiddenworldsgames.com
```

Internal Render address:

```text
gondwana-mcp:8080
```

### Build

```text
Root Directory:                 blank
Dockerfile Path:                Tooling/Gondwana.Mcp/Dockerfile
Docker Build Context Directory: .
Registry Credential:            none
Build Filters / Included Paths: none
Build Filters / Ignored Paths:  none
```

The Git provider is connected directly to the GitHub repository.

There is no `render.yaml`/Render Blueprint in the repository at the time of writing. The production Render configuration therefore lives primarily in the **Render dashboard**, not in source control.

### Deploy

```text
Docker Command override:  none
Pre-Deploy Command:       none
Auto-Deploy:              On Commit
PR Previews:              Off
```

### Networking/cache

```text
Edge Cache Profile: None
```

### Health checks

```text
Health Check Path: /health
```

### Maintenance

```text
Maintenance Mode: disabled
```

### Notifications

The service currently uses the workspace default:

```text
Only failure notifications
```

### Deploy hook

Render provides a private deploy-hook URL.

**Do not place the actual deploy-hook URL in this document, source control, an issue, or a public wiki page.**

If compromised, regenerate it in the Render dashboard.

---

## 9. Production Environment Variables / Secrets

The supplied Render settings snapshot did not include the Environment page, so verify these separately in Render.

### GitHub token

Expected setting:

```text
GondwanaMcp__GitHubToken=<read-only GitHub token>
```

Purpose:

- Enables `search_repository`.
- Remains server-side.
- Is never intentionally returned to MCP callers.

The service can still perform ordinary public repository list/read operations without it.

### AllowedHosts

`appsettings.json` defaults to:

```json
"AllowedHosts": "localhost;127.0.0.1"
```

Production should override this as appropriate for the deployed hostname, for example:

```text
AllowedHosts=mcp.hiddenworldsgames.com
```

The MCP README explicitly recommends using the exact production hostname rather than a wildcard in production.

### OpenAI domain challenge token

When required by the OpenAI submission/review process:

```text
GondwanaMcp__OpenAiAppsChallengeToken=<exact portal-issued token>
```

After configuration and deployment, verify:

```text
https://mcp.hiddenworldsgames.com/.well-known/openai-apps-challenge
```

The response must contain only the exact token as plain text.

Never commit this token.

---

## 10. Render Auto-Deploy Behavior

Current setting:

```text
Auto-Deploy: On Commit
Branch:      master
```

Render's `On Commit` mode triggers a deployment whenever the linked branch is updated.

Because the current service has:

```text
Root Directory: blank
Included Paths: none
Ignored Paths:  none
```

**every commit merged or pushed to `master` currently causes Render to rebuild/redeploy `gondwana-mcp`.**

That includes commits that only change:

- documentation
- demos
- assets
- Studio
- engine code unrelated to MCP
- `docs/ai/repository-map.md`

### This is more deployment than the MCP technically needs

Repository source/doc files are fetched from GitHub at request time.

For example, if this file changes:

```text
docs/ai/repository-map.md
```

then after the change is merged to `master`, an MCP repository read will see the new version without needing the service itself to be rebuilt.

However, under the current Render settings, that same merge will also trigger a Docker rebuild simply because it changed `master`.

That rebuild is unnecessary but harmless.

### Actual MCP code changes

If a change modifies something such as:

```text
Tooling/Gondwana.Mcp/Program.cs
Tooling/Gondwana.Mcp/Services/*
Tooling/Gondwana.Mcp/Tools/*
Tooling/Gondwana.Mcp/Configuration/*
Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj
Tooling/Gondwana.Mcp/Dockerfile
```

then a rebuild **is** required.

With the current settings, the workflow is automatic:

```text
merge PR to master
        ↓
Render detects new master commit
        ↓
Render builds Tooling/Gondwana.Mcp/Dockerfile
        ↓
dotnet restore / publish
        ↓
new Docker image starts
        ↓
Render health-checks /health
        ↓
new MCP implementation becomes live
```

No manual Render action is normally necessary.

### Manual redeployment

If an automatic deployment fails, is skipped, or a maintainer simply wants to rebuild the current `master`, use:

```text
Render Dashboard
  → gondwana-mcp
  → Manual Deploy
  → Deploy latest commit
```

Do not confuse this with repository content freshness. A manual deploy is only necessary to refresh the running application/container, not to make normal repository source changes readable.

---

## 11. Optional Future Improvement: Add Render Build Filters

Because Gondwana is a monorepo, the current Render configuration rebuilds the MCP service much more frequently than necessary.

A sensible future optimization is to keep:

```text
Root Directory: blank
Docker Build Context: .
```

but add Render **Included Paths**.

A reasonable starting set is:

```text
Tooling/Gondwana.Mcp/**
Directory.Build.props
Directory.Build.targets
global.json
```

This would make changes to the actual MCP application or root build configuration trigger a deployment, while a normal change to Spot, Studio, assets, documentation, etc. would not.

This is preferable to changing the Root Directory because the Dockerfile currently depends on repository-root files.

If additional root-level build files are introduced later, update the Included Paths accordingly.

### Alternative auto-deploy mode

Render also supports:

```text
After CI Checks Pass
```

instead of:

```text
On Commit
```

That may be worth considering if deployment should wait for GitHub Actions checks on the new `master` commit.

If branch protection already guarantees that only passing PRs can merge, `On Commit` remains a simple and reasonable configuration.

---

## 12. Custom Domain Setup

The production MCP service uses:

```text
mcp.hiddenworldsgames.com
```

The general Render setup process is:

1. Create/deploy the Render web service.
2. In the service's **Custom Domains** settings, add:
   ```text
   mcp.hiddenworldsgames.com
   ```
3. In the DNS provider for `hiddenworldsgames.com`, create the DNS record Render requests.
4. For a normal subdomain such as `mcp`, this is typically a `CNAME` pointing to the service's Render hostname.
5. Return to Render and verify the custom domain.
6. Render provisions and renews TLS automatically.
7. Verify:
   ```text
   https://mcp.hiddenworldsgames.com/
   https://mcp.hiddenworldsgames.com/health
   https://mcp.hiddenworldsgames.com/mcp
   ```

Render automatically manages HTTPS/TLS for verified custom domains.

Current Render documentation:

- Custom domains: https://render.com/docs/custom-domains
- DNS configuration: https://render.com/docs/configure-other-dns
- TLS: https://render.com/docs/tls

---

## 13. Plugin Connection to the MCP Service

The Gondwana plugin points to the production MCP endpoint through:

```text
plugins/gondwana-game-engine/.mcp.json
```

Current contents:

```json
{
  "mcpServers": {
    "gondwana": {
      "type": "http",
      "url": "https://mcp.hiddenworldsgames.com/mcp"
    }
  }
}
```

File:

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/.mcp.json

The plugin package metadata is in:

```text
plugins/gondwana-game-engine/.codex-plugin/plugin.json
```

File:

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/.codex-plugin/plugin.json

The plugin's three current skills are:

```text
create-gondwana-game
debug-gondwana-game
explain-gondwana-api
```

under:

```text
plugins/gondwana-game-engine/skills/
```

The MCP server is the repository/documentation access layer used by those workflows.

---

## 14. Documentation That Supports the MCP/Plugin

### MCP README

```text
Tooling/Gondwana.Mcp/README.md
```

Primary service-level documentation.

https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Mcp/README.md

### AI documentation index

```text
docs/ai/README.md
```

Explains repository-aware agent guidance and points into the repository/documentation maps.

https://github.com/Isthimius/Gondwana/blob/master/docs/ai/README.md

### Repository map

```text
docs/ai/repository-map.md
```

Provides AI agents with a high-level map of major Gondwana runtime, adapter, demo, test, and tooling areas.

https://github.com/Isthimius/Gondwana/blob/master/docs/ai/repository-map.md

This file is **repository content**, not server application code. Updating it does not inherently require a new MCP binary.

### Documentation map

```text
docs/ai/documentation-map.md
```

Routes agents to relevant wiki/documentation topics.

https://github.com/Isthimius/Gondwana/blob/master/docs/ai/documentation-map.md

### Implementation workflow

```text
docs/ai/implementation-workflow.md
```

Describes how agents should investigate source, implement changes, and validate work.

https://github.com/Isthimius/Gondwana/blob/master/docs/ai/implementation-workflow.md

### Plugin README

```text
plugins/gondwana-game-engine/README.md
```

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/README.md

### Privacy policy

```text
plugins/gondwana-game-engine/PRIVACY.md
```

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/PRIVACY.md

### Terms

```text
plugins/gondwana-game-engine/TERMS.md
```

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/TERMS.md

### Support

```text
plugins/gondwana-game-engine/SUPPORT.md
```

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/SUPPORT.md

### OpenAI submission notes

```text
plugins/gondwana-game-engine/SUBMISSION.md
```

This is the maintainer checklist for the plugin submission and includes domain verification, expected tool annotations, positive/negative test cases, and production-host readiness.

https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/SUBMISSION.md

---

## 15. Tests

MCP tests live under:

```text
Testing/Gondwana.Tests/GondwanaMcp/
```

Important files include:

```text
GitHubRepositoryServiceTests.cs
GondwanaWikiServiceTests.cs
McpToolMetadataTests.cs
TestHttpMessageHandler.cs
```

The tests verify areas such as:

- GitHub repository service behavior
- Wiki retrieval/search behavior
- MCP metadata
- Read-only/destructive/idempotent/open-world annotations
- Service limits and expected error cases

Directory:

https://github.com/Isthimius/Gondwana/tree/master/Testing/Gondwana.Tests/GondwanaMcp

The MCP project itself is referenced by the main Gondwana test project so these tests run with the regular test suite.

---

## 16. Default Service Limits

Current `appsettings.json`:

```json
{
  "AllowedHosts": "localhost;127.0.0.1",
  "GondwanaMcp": {
    "MaxFileBytes": 524288,
    "MaxLinesPerRead": 400,
    "MaxSearchResults": 20,
    "WikiCacheMinutes": 15,
    "WikiSearchConcurrency": 6
  }
}
```

Meaning:

| Setting | Current value | Purpose |
|---|---:|---|
| `MaxFileBytes` | 524288 | Maximum file/wiki size returned by the service. |
| `MaxLinesPerRead` | 400 | Maximum repository lines returned per file-read call. |
| `MaxSearchResults` | 20 | Upper bound for search results. |
| `WikiCacheMinutes` | 15 | In-memory wiki cache duration. |
| `WikiSearchConcurrency` | 6 | Maximum concurrent wiki fetches during search. |

`Program.cs` also validates safe ranges for these settings at startup.

---

## 17. Production Verification Checklist

After an MCP implementation deployment, check the following.

### 1. Render deployment

Confirm the latest deployment is:

```text
Live
```

and corresponds to the expected `master` commit.

### 2. Health

Open:

```text
https://mcp.hiddenworldsgames.com/health
```

Confirm:

```text
status = ok
repository = Isthimius/Gondwana
access = read-only
```

### 3. Root information endpoint

Open:

```text
https://mcp.hiddenworldsgames.com/
```

Confirm that:

- repository is `Isthimius/Gondwana`
- default ref is `master`
- MCP endpoint is `/mcp`
- health endpoint is `/health`
- authenticated search is available if the GitHub token is configured

### 4. MCP client/tool scan

Verify all seven tools are visible:

```text
get_repository_info
list_repository
read_repository_file
search_repository
list_wiki_pages
read_wiki_page
search_wiki
```

### 5. Tool metadata

Confirm all seven remain:

```text
readOnlyHint:    true
destructiveHint: false
idempotentHint:  true
openWorldHint:   false
```

### 6. Repository freshness

Make a repository read against a known file on `master` and confirm the returned commit/content matches the current repository.

### 7. Search authentication

Run a simple `search_repository` request.

If it reports authentication unavailable, verify:

```text
GondwanaMcp__GitHubToken
```

in Render.

### 8. Wiki access

Run:

```text
list_wiki_pages
```

and read/search at least one wiki page.

Remember that wiki data can remain in memory cache for up to the configured `WikiCacheMinutes`.

### 9. OpenAI challenge endpoint

Only when applicable:

```text
https://mcp.hiddenworldsgames.com/.well-known/openai-apps-challenge
```

Confirm the exact expected token is returned.

---

## 18. Recreating the Render Service From Scratch

If the existing Render service disappears and must be recreated:

### Step 1 — Create a new Render Web Service

Connect the GitHub repository:

```text
Isthimius/Gondwana
```

Choose branch:

```text
master
```

Use:

```text
Runtime / deployment type: Docker
```

### Step 2 — Docker configuration

Leave:

```text
Root Directory: blank
```

Set:

```text
Dockerfile Path:
Tooling/Gondwana.Mcp/Dockerfile
```

Set build context:

```text
.
```

Do not change the build context to the MCP subdirectory unless the Dockerfile is also redesigned.

### Step 3 — Instance/region

Current production choices are:

```text
Region: Virginia (US East)
Plan:   Starter
```

These are operational choices rather than application requirements.

### Step 4 — Environment/secrets

Configure at minimum the production values needed for:

```text
AllowedHosts
GondwanaMcp__GitHubToken
```

If OpenAI domain verification is active, also configure:

```text
GondwanaMcp__OpenAiAppsChallengeToken
```

Never commit any secret token.

### Step 5 — Health check

Configure:

```text
/health
```

### Step 6 — Auto deploy

Current production configuration:

```text
On Commit
```

Optionally configure build filters as described earlier.

### Step 7 — Deploy

Allow Render to build:

```text
Tooling/Gondwana.Mcp/Dockerfile
```

The container should bind to:

```text
8080
```

via the Dockerfile's:

```text
ASPNETCORE_URLS=http://+:8080
```

### Step 8 — Verify Render hostname

Before adding the custom hostname, verify the service through the Render-generated `onrender.com` hostname.

Test:

```text
/
 /health
 /mcp
```

### Step 9 — Add custom domain

Add:

```text
mcp.hiddenworldsgames.com
```

to the service's Render Custom Domains configuration.

Update DNS with the provider for `hiddenworldsgames.com`, verify the domain in Render, and confirm TLS is active.

### Step 10 — Verify public endpoints

Confirm:

```text
https://mcp.hiddenworldsgames.com/
https://mcp.hiddenworldsgames.com/health
https://mcp.hiddenworldsgames.com/mcp
```

### Step 11 — Verify plugin endpoint

Confirm this still points to the same production URL:

```text
plugins/gondwana-game-engine/.mcp.json
```

Expected:

```json
"url": "https://mcp.hiddenworldsgames.com/mcp"
```

### Step 12 — Re-run plugin/MCP verification

Use `SUBMISSION.md` as the authoritative checklist for plugin-related review requirements and domain verification.

---

## 19. Common Maintenance Scenarios

### “I changed `docs/ai/repository-map.md`. Do I need to rebuild the MCP?”

Technically, **no**.

The MCP reads repository files live from GitHub, so once the change reaches `master`, the service can read the new file immediately.

With the current Render settings, however, the commit will also trigger a Render rebuild because all `master` commits trigger auto-deploy.

### “I changed `GitHubRepositoryService.cs`. Do I need to deploy?”

Yes.

That is application code. The running container must be replaced with a new build.

Current Render configuration does this automatically after the change reaches `master`.

### “I changed `.mcp.json` to point to a different hostname.”

That changes the **client/plugin connection configuration**, not the hosted MCP implementation itself.

The new target server must already exist and be working.

### “I changed a wiki page.”

The MCP wiki service fetches public wiki content remotely.

The updated page may remain cached for up to the configured wiki cache duration, currently 15 minutes.

No service deployment is required solely to refresh wiki content.

### “`search_repository` stopped working but reads still work.”

Most likely check:

```text
GondwanaMcp__GitHubToken
```

Ordinary public list/read operations do not require the token. GitHub code search does.

### “The service returns a host error in production.”

Check:

```text
AllowedHosts
```

and verify that production allows:

```text
mcp.hiddenworldsgames.com
```

### “Render is redeploying for unrelated Gondwana commits.”

That is expected with the current configuration:

```text
Root Directory: blank
Build Filters:  none
Auto-Deploy:    On Commit
```

Add Included Paths if this becomes annoying.

### “The service builds locally but Render Docker build fails.”

Confirm:

```text
Dockerfile Path = Tooling/Gondwana.Mcp/Dockerfile
Build Context   = .
Root Directory  = blank
```

The Dockerfile needs access to root-level build files.

---

## 20. Security / Design Constraints That Should Not Be Accidentally Removed

These constraints are intentional.

### Keep the repository hard-coded

Do not turn repository owner/name into arbitrary MCP tool parameters.

The closed-world boundary is a security and plugin-review feature.

### Keep the server read-only

Do not casually add:

- issue creation
- pull request creation
- commits
- branch writes
- arbitrary GitHub writes

The plugin/service contract is explicitly read-only.

### Do not expose the GitHub token

The GitHub token exists only to enable server-side code search.

Never:

- return it in a tool result
- write it to logs
- commit it
- place it in documentation

### Keep bounded reads

`MaxFileBytes`, `MaxLinesPerRead`, and search-result limits exist deliberately.

Avoid turning the MCP service into an unrestricted repository dump endpoint.

### Preserve closed-world tool annotations

The tools are intentionally marked:

```text
ReadOnly = true
Destructive = false
Idempotent = true
OpenWorld = false
```

Changes here can affect plugin review/security behavior.

### Do not add generic URL fetch

The service is intended to provide bounded access to Gondwana's official repository and wiki, not act as an arbitrary web proxy.

---

## 21. External Documentation

Render documentation relevant to the current setup:

- Web services: https://render.com/docs/web-services
- Deploys and auto-deploy: https://render.com/docs/deploys
- Monorepo/build filters: https://render.com/docs/monorepo-support
- Custom domains: https://render.com/docs/custom-domains
- DNS configuration: https://render.com/docs/configure-other-dns
- TLS: https://render.com/docs/tls

GitHub repository:

- https://github.com/Isthimius/Gondwana

Production MCP:

- https://mcp.hiddenworldsgames.com/mcp

---

## 22. Short Version

If everything is already configured and working, normal maintenance is simple:

```text
Repository/docs change
    → merge to master
    → MCP reads new content live from GitHub
    → no new MCP binary is technically required

Actual Tooling/Gondwana.Mcp code change
    → merge to master
    → Render Auto-Deploy detects commit
    → Dockerfile rebuilds service
    → /health passes
    → new MCP code goes live
```

Under the **current** Render configuration, both kinds of changes also trigger a Render rebuild because the service watches all changes to `master`.

If unnecessary rebuilds become bothersome, keep the repository-root Docker build context but add Render Included Paths for the MCP project and the root build files it depends on.
