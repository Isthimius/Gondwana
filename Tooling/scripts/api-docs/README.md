# Versioned API documentation

- [Stable API](https://isthimius.github.io/Gondwana/api/) (`/api/`) redirects to the highest published stable release.
- [Development API](https://isthimius.github.io/Gondwana/api/latest/) (`/api/latest/`) documents current `master`, explicitly marked development/unreleased.
- `/api/vX.Y.Z/` is an immutable release snapshot. Use the header's **API version** selector for historical releases, current stable, and development.
- The [legacy root](https://isthimius.github.io/Gondwana/) is a compatibility redirect to `api/` after migration. Both entry points use `window.location.replace`, immediate meta refresh, and a fallback link. The root never contains a hard-coded release.

The header displays the injected Doxygen `PROJECT_NUMBER`: `Version X.Y.Z` for a release or `Development (master) - <NBGV version>` for development. On development pages, the header also links directly to the latest stable release. Switching versions attempts the same relative HTML page and preserves its query and member anchor; a missing page or failed lookup falls back to the selected version's home. Member anchors can change between releases; if only the anchor disappears, the corresponding class page still opens.

## Build and publication

`docs.yml` runs for relevant `master` pushes and manual dispatch on `master`. It checks out current `master` explicitly. C# sources, project files, MSBuild props/targets, version/SDK configuration, Doxygen assets, and API publishing scripts trigger it. Tests, demos, tools, and the other Doxygen-excluded source trees do not; README/changelog/wiki-only changes do not. These exclusions mirror `EXCLUDE_PATTERNS` in the Doxyfile. If Doxygen inputs expand to another language or source tree, update the trigger filters with them.

The release workflow's separate `api-docs` job runs after successful release publication, checks out the triggering tag, and accepts only canonical stable `vX.Y.Z` tags. It injects the tag version rather than guessing from NBGV. Failed documentation jobs can be retried independently of successful packaging jobs.

`build.py` generates the installed Doxygen's default header with `doxygen -w html`, adds the version navigation, and uses supported `HTML_HEADER`, `HTML_EXTRA_FILES`, and `HTML_EXTRA_STYLESHEET` settings. `--source-root` changes only the source input; configuration, branding, CSS, JavaScript, and header customization always come from the tooling checkout. UTF-8 is the default; non-UTF-8 C# files receive Doxygen Windows-1252 input overrides for legacy punctuation without modifying source files. Generated output must be empty and outside the source directory. No Doxygen fork or third-party UI library is required. See [Doxygen configuration](https://www.doxygen.nl/manual/config.html).

`publish.sh` fetches the latest `gh-pages`, applies `publish.py` to a disposable worktree, and performs a normal fast-forward push. On a concurrent update it fetches again and reapplies the scoped update, up to five times. Normal publishing never replaces the whole branch. Development, release documentation, and migration share the `api-docs-publication` concurrency group. The workflows explicitly request a Pages build after pushing because a `GITHUB_TOKEN` push does not itself trigger one. Pages must be configured to deploy from `gh-pages`, `/`.

The API publisher owns only:

| Path | Policy |
| --- | --- |
| `api/latest/` | Replace completely on development publication, including removing stale development files |
| `api/vX.Y.Z/` | Create once; leave existing snapshots byte-for-byte unchanged on every rerun |
| `api/versions.json` | Rebuild from existing canonical release directories with an `index.html`; unique, numerically sorted newest-first |
| `api/index.html` | Release publication only; redirect to the highest published release, preventing old reruns from downgrading stable |

Normal publishing preserves everything outside these paths, including the root compatibility redirect and `.nojekyll`. Release snapshots load the shared manifest without caching so newly published versions appear in older selectors without changing their files. URLs are relative to the copied script's location, preserving the `/Gondwana/` Pages base path even on deep pages. The GitHub Wiki is a separate repository and is never touched.

## Historical archive and one-time migration

The complete 16-release archive covers v2.1.0 through v2.5.2: 2.1.0–2.1.2, 2.2.0–2.2.4, 2.3.0, 2.4.0–2.4.3, and 2.5.0–2.5.2. It has been built in local staging; publication is deliberately a separate, manually authorized operation. See [validation evidence](history-validation.md). After successful dispatch, these directories plus `latest/`, `versions.json`, and `api/index.html` form the archive. The only other root files are `index.html` and `.nojekyll`.

The old root site's v2.5.3 label was unreleased development for v2.6.0, not a release. It is removed, never archived or relabeled. v2.0.1 is intentionally excluded. Historical tags supply only exact source snapshots through `git archive`; the current configuration is applied uniformly. No historical workflow, package build, release branch, GitHub Release, NuGet artifact, changelog, or tag is changed.

The initial inspection recorded `gh-pages` at `b67c960918d46858a1c5f166350322060dede084`. Recovery branch `gh-pages-pre-versioning-20260908` was created and verified at that commit before local generation. **Retain it.** The actual migration records and backs up the then-current Pages commit too; if it has advanced, the new run's backup is the appropriate immediate rollback point. Never overwrite an existing backup with different content.

`rebuild.py` audits legacy paths against the inspected commit's exact Git entries. Removing its copied `ai/`, `doxy/`, and `logo/` files was explicitly approved, along with generated Doxygen output. New or changed legacy files, new release directories, or other unexpected API entries stop the migration for review. Ordinary pre-migration `api/latest/` updates are allowed. A second migration after success intentionally fails the audit.

The script records tag commit IDs and current master, verifies a remote backup, builds all releases and then development in isolated temporary source directories, validates the complete site, and writes `report.json`. Every version includes `source.json` with its exact source SHA. Doxygen logs and evidence stay outside the site. Publication revalidates the tree and its digest, checks the backup, and creates one parentless commit through a temporary Git index. The only `gh-pages` mutation is one push with `--force-with-lease=refs/heads/gh-pages:<recorded-SHA>`. A concurrent writer causes an abort even if it wins after the preflight read. There is no delete-first operation or partial history publication.

The site must stay at or below 750 MiB; any file at or above 100 MiB also aborts. This is below GitHub's [1 GB published-site limit and 10-minute deployment timeout](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits). With about 20,700 static files and no Jekyll processing, branch-based publication is reasonable; actual hosted deployment time is verified by the dispatched workflow, not claimed from local tests.

## Review, dispatch, verification, and recovery

1. Review and merge the implementation PR into `master`. Review the exact tag range, approved legacy removal, backup conflict behavior, lease-protected push, and validation evidence. Merging runs normal development publishing only; it does not rebuild history. Let that Docs run finish before dispatch.
2. Open Actions → **One-time API history rebuild** → **Run workflow**, select `master`, and enter exactly `REBUILD_GH_PAGES`. Wrong text or another branch cannot run the job. Leave `backup_ref` empty for the UTC-date default, or use an unused `refs/heads/gh-pages-pre-versioning-YYYYMMDD-suffix` if the date's ref already backs up an older commit. For example, use `refs/heads/gh-pages-pre-versioning-20260908-migration` for a same-day run after the initial backup. A conflict stops; do not delete or overwrite the earlier backup.
3. Monitor the run's preparation, generation, real-site browser checks, measured summary, atomic publication, and Pages-build check. Download **api-history-evidence**, especially `state.json`, `report.json`, `published.json`, and the Doxygen logs. A failure before publication leaves `gh-pages` unchanged. A failure after publication requires inspecting `published.json` and the remote ref before any retry.
4. Open the root URL and confirm automatic arrival at `/Gondwana/api/v2.5.2/`; open `/api/` directly too. Inspect `api/versions.json`, all 16 release homes, and development's unreleased label and stable link. Switch a class/member page among v2.1.0, v2.3.0, v2.5.2, and development. Verify a missing page falls back to the selected home. No v2.5.3 directory should exist. Compare `api/latest/source.json` with the run's recorded master SHA. The workflow polls the Pages build for the published commit for up to ten minutes.
5. If rollback is needed, first pause Docs and release documentation publishers and let active runs finish. Use the backup **from that migration's `state.json`** and the published commit from `published.json`. Verify both remote refs before the following Bash commands. The lease must name the commit you have reviewed; if it fails, inspect concurrent changes instead of weakening it:

   ```bash
   backup_ref=refs/heads/gh-pages-pre-versioning-YYYYMMDD-suffix
   published_sha=REPLACE_WITH_PUBLISHED_COMMIT
   git fetch origin "$backup_ref"
   backup_sha=$(git rev-parse FETCH_HEAD)
   git ls-remote --heads origin gh-pages "$backup_ref"
   git push --force-with-lease="refs/heads/gh-pages:$published_sha" origin "$backup_sha:refs/heads/gh-pages"
   gh api --method POST repos/Isthimius/Gondwana/pages/builds
   ```

   Verify the Pages build and restored site, then resume normal publishers. The backup ref remains intact. Rollback restores the old site's contents, including its old root label; it does not convert that label into a real release.

6. After successful live verification, disable **One-time API history rebuild** in Actions (or run `gh workflow disable api-history-rebuild.yml --repo Isthimius/Gondwana`), then remove that workflow in a follow-up PR. `rebuild.py` and its dedicated tests may also be removed after retaining evidence and this recovery guide. Keep the backup refs, permanent `build.py`/`publish.py` improvements, selector tests, link corrections, and normal workflow changes.

When v2.6.0 is released, the ordinary release job creates `/api/v2.6.0/`, updates the shared manifest and `/api/index.html`, and leaves all older directories unchanged. Development regeneration replaces only `api/latest/` and updates development availability in the shared manifest. The root continues to redirect to `api/`. Snapshots are retained indefinitely; there is no pruning policy.

## Local validation

Requires Python 3, Node.js, and Doxygen for the build smoke test:

```sh
python3 -m unittest discover -s Tooling/scripts/api-docs -p 'test_*.py' -v
node --check docs/doxy/api-versions.js
# With Playwright and Chromium installed:
node Tooling/scripts/api-docs/test-selector.cjs
node Tooling/scripts/api-docs/test-site.cjs /tmp/archive/site
bash -n Tooling/scripts/api-docs/publish.sh
python3 Tooling/scripts/api-docs/build.py --version 'Development (master) - 2.6.0-test' --output /tmp/gondwana-api-build
```

`publish.py --site <test-site> --source <build>/html --version latest` can simulate publication without Git or network access. Repeat with two release tags to verify retention and the stable redirect. Serve the test site under `/Gondwana/` over HTTP to test manifest loading and deep-page switching (opening files with `file://` does not support fetch).
