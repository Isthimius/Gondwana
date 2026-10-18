# Wiki synchronization

This utility keeps the repository's `docs/wiki/` content and the
[Gondwana GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki) in sync.
It is shared by the event-driven and weekly GitHub Actions workflows.

## Files

| File | Role |
| --- | --- |
| `sync.py` | Conflict-aware, bidirectional synchronization and GitHub PR handling |
| `test_sync.py` | Isolated Git integration tests (no GitHub credentials or remote writes) |
| `baseline.json` | Checkpoint of the last imported Wiki revision and source repository revision |

## How synchronization runs

- [Wiki sync](../../../.github/workflows/wiki-sync.yml) publishes repository
  edits after they reach `master`, and imports Wiki edits through a PR on
  `automation/wiki-sync`.
- [Wiki reconciliation (weekly)](../../../.github/workflows/wiki-reconcile-weekly.yml)
  compares both sides again to recover missed events or incomplete runs.
- Normal synchronization uses whole-file three-way comparisons. Independent
  changes to the same file fail with a conflict rather than silently overwriting
  either version. No Wiki import writes directly to `master`.

For routine use, edit the Wiki or `docs/wiki/` and let GitHub Actions handle
synchronization. To explicitly recover a divergent copy, use **Actions → Wiki
sync → Run workflow** and select `repo-to-wiki` or `wiki-to-repo`.
That manual workflow uses `--authoritative` and can replace or delete
destination content; review both sides first.

## Running and testing

The synchronization script requires Python 3.11+, Git, GitHub CLI (`gh`),
`GITHUB_REPOSITORY=Isthimius/Gondwana`, and `WIKI_SYNC_TOKEN` with Wiki Git
write access plus repository Contents and Pull requests write permissions.
It performs remote writes; use the Actions workflow for normal operation.

From the repository root, run its tests without credentials:

```console
python3 -m unittest discover -s Tooling/scripts/wiki-sync -p 'test_*.py' -v
```

For the full editing, checkpoint, conflict-resolution, authentication, and
manual-recovery procedures, see
[Wiki synchronization and recovery](../../../docs/wiki/README.md).
