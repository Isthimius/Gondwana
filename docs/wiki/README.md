# Editing and synchronizing the Gondwana Wiki

`docs/wiki` mirrors the [Gondwana GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki).
Both this directory and GitHub's Wiki UI are supported editing surfaces. Changes
synchronize automatically in both directions. Article filenames, assets, and
special files such as `Home.md`, `_Sidebar.md`, and `_Footer.md` retain their Wiki
layout. This repository-only `README.md` is **never published to the Wiki**;
the root Wiki name `README.md` is reserved and excluded in both directions.

## Normal synchronization

- Repository changes publish promptly after merging into protected `master`.
- Wiki UI creates/edits trigger `gollum` and create or update a PR against `master`
  on the single `automation/wiki-sync` branch. The title begins `docs(wiki):`.
- Auto-merge is enabled through GitHub's API; normal CI, branch protection, and
  required human reviews still govern the merge. No Wiki import pushes to master.
- Every run compares actual Git content. No content change means no new content
  commit; repeat events do not depend on token-based event suppression.

The shared Python script compares each file against the last acknowledged import
or publication. Unrelated edits from the other surface survive. Independently
edited versions of the same file (including delete/edit conflicts) fail the
workflow clearly before publishing or pushing the import. No version is silently
selected. A conflict can occur even for separate edits within one article; this
conservative whole-file policy also protects binary assets.

An existing automation PR is merged with current master locally before updating;
a Git conflict leaves the remote PR untouched and the workflow failed. Resolve
the conflict through the normal PR process and rerun. Do not add unrelated code
to the automation branch. The synchronization script rejects it.

## Manual synchronization

In **Actions → Wiki sync → Run workflow**, choose:

| Direction | Authoritative source | Destination |
| --- | --- | --- |
| `repo-to-wiki` | Current master `docs/wiki` | GitHub Wiki |
| `wiki-to-repo` | Current GitHub Wiki | Shared automation branch/PR |

These explicit recovery operations replace destination content, including
deletions, while excluding this README. Review both versions before selecting an
authoritative direction. They always use the script from master. They cannot
bypass a Git merge conflict already present between the automation PR and master;
resolve that PR conflict first.

## Weekly safety net

**Wiki reconciliation (weekly)** runs Monday at **04:17 UTC** and can also be
started manually. It exists only to catch changes/events missed by the normal
event-driven synchronization: deletions, incomplete rename events, failed or
interrupted runs, and other drift. It compares complete trees, imports Wiki-only
changes through the same PR, and retries safe repository publishing. A rename is
represented by deletion plus addition if Git does not identify it as a rename.
There are no permanent stale copies. Ambiguous two-sided changes fail visibly.
Equivalent trees cause no content commit or new PR.

Both workflows share a concurrency group. They read current heads, not just the
event's file list, so pending events coalesced by Actions are covered by the next
run. Git push checks reject external edits made during a run; rerun after a race.

## Authentication, checkpoints, and release handling

`WIKI_SYNC_TOKEN` authenticates Wiki Git access and the repository branch/PR API.
It must have working Wiki write access and repository Contents and Pull requests
read/write permissions. The PAT enables normal downstream PR workflows, unlike
pushes using the default Actions token. It is passed through the environment and
Git askpass, never embedded in remote URLs or printed in diagnostics.

`Tooling/scripts/wiki-sync/baseline.json` records the imported Wiki commit and,
on subsequent imports, the master revision used to prepare the PR.
Automated Wiki publication commits record a `Gondwana-Source:` master SHA.
Together these identify the common content without publishing infrastructure
files into the Wiki. Keep the Wiki history intact; rewritten history requires
explicit manual recovery. The initial checkpoint is the exact Wiki revision
imported in this migration; later Wiki edits are retained on first publication.

Import checkpoints live outside `docs/**` so the existing CI path filters do not
skip Wiki import PRs. The existing documentation labeler and release configuration
exclude these PRs from GitHub release notes. The explicit `^docs\(wiki\):` parser
rule in `cliff.toml` excludes automation commits from root and project changelogs;
normal documentation commits keep their existing behavior.

For local validation, run:

```console
python3 -m unittest discover -s Tooling/scripts/wiki-sync -p 'test_*.py' -v
```
