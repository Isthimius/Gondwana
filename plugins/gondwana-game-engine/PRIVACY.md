# Gondwana Game Engine Plugin Privacy Policy

Last updated: August 26, 2026

This policy covers the public Gondwana Game Engine plugin and the read-only Gondwana MCP service at `https://mcp.hiddenworldsgames.com/mcp`.

## What the service receives

The Gondwana MCP service receives only the tool arguments needed for a requested repository or documentation lookup, such as:

- Gondwana repository paths
- branch, tag, or commit references
- line-range limits
- repository code-search terms
- Gondwana wiki page names and search terms

The service does not require an end-user Gondwana account, GitHub account, or OAuth login.

When the plugin is used through ChatGPT or Codex, OpenAI separately processes the user's conversation and decides which MCP tool arguments to send. OpenAI's handling of that conversation is governed by OpenAI's own terms and privacy policies.

## How data is used

Tool arguments are used only to perform the requested read-only lookup against the official `Isthimius/Gondwana` repository and Gondwana GitHub wiki, enforce service limits, and return the result.

The service does not use tool arguments for advertising, profiling, sale of personal data, or training a separate Gondwana model.

## Recipients and service providers

The service relies on:

- **Render**, which hosts the Gondwana MCP process and may process network and operational metadata needed to run the service.
- **GitHub**, which receives repository and wiki API requests made by the server.
- **OpenAI**, when the plugin is invoked from ChatGPT or Codex, which transmits selected MCP tool arguments to the service.

Those providers process data under their own applicable terms and privacy policies.

## Retention

The Gondwana MCP application has no user database and does not persist user accounts, conversation history, MCP tool arguments, or MCP tool results after the request completes.

The application may cache public Gondwana wiki content in memory for performance. That cache contains public documentation rather than user-specific history and expires automatically or is cleared when the process restarts.

Render retains service logs according to the retention period of the hosting workspace. At the time this policy was published, Render documents a 7-day log-retention period for Hobby workspaces. If the hosting plan changes, Render's then-current retention policy applies.

GitHub and OpenAI may retain data they process according to their own policies. Gondwana does not control those providers' retention periods.

## Personal and sensitive information

The plugin is designed for public game-engine source and documentation. Do not include passwords, access tokens, private source code, confidential information, or unnecessary personal data in repository/wiki search terms or other MCP arguments.

The server-side GitHub token used for code search is owned by the service and is never intentionally returned to MCP clients.

## User controls

Because Gondwana does not create user accounts or persist user-specific MCP request history, there is normally no Gondwana-held user profile or request archive to delete.

Users can stop using or uninstall the plugin at any time. Questions or privacy requests can be raised through the support channels listed in `SUPPORT.md`.

## Changes

This policy may be updated when the plugin, hosting model, or data practices change. Material changes will be reflected in this file and its update date.
