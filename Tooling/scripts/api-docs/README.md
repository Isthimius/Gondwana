# Versioned API documentation

- [Stable API](https://isthimius.github.io/Gondwana/api/) (`/api/`) redirects to the highest published stable release.
- [Development API](https://isthimius.github.io/Gondwana/api/latest/) (`/api/latest/`) documents current `master`, explicitly marked development/unreleased.
- `/api/vX.Y.Z/` is an immutable release snapshot. Use the header's **API version** selector for historical releases, current stable, and development.
- The [legacy root](https://isthimius.github.io/Gondwana/) is a compatibility redirect to `api/`. Both entry points use `window.location.replace`, immediate meta refresh, and a fallback link. The root never contains a hard-coded release.

The header displays the injected Doxygen `PROJECT_NUMBER`: `Version X.Y.Z` for a release or `Development (master) - <NBGV version>` for development. On development pages, the header also links directly to the latest stable release. Switching versions attempts the same relative HTML page and preserves its query and member anchor; a missing page or failed lookup falls back to the selected version's home. Member anchors can change between releases; if only the anchor disappears, the corresponding class page still opens.

## Build and publication

`docs.yml` runs for relevant `master` pushes and manual dispatch on `master`. It checks out current `master` explicitly. C# sources, project files, MSBuild props/targets, version/SDK configuration, Doxygen assets, and API publishing scripts trigger it. Tests, demos, tools, and the other Doxygen-excluded source trees do not; README/changelog/wiki-only changes do not. These exclusions mirror `EXCLUDE_PATTERNS` in the Doxyfile. If Doxygen inputs expand to another language or source tree, update the trigger filters with them.

The release workflow's separate `api-docs` job runs after successful release publication, checks out the triggering tag, and accepts only canonical stable `vX.Y.Z` tags. It injects the tag version rather than guessing from NBGV. Failed documentation jobs can be retried independently of successful packaging jobs.

Both permanent API workflows (`docs.yml` and `release.yml`) use `install-doxygen.sh` to install the official Linux x64 **Doxygen 1.14.0** archive, verify its pinned SHA-256 before extraction, and check the executable version and configuration generation before adding it to PATH. Do not replace this with the distribution's unpinned `apt` package. To upgrade, review the official archive and checksum and rerun the real-Doxygen encoding and browser tests on Linux.

`build.py` generates the installed Doxygen's default header with `doxygen -w html`, adds the version navigation, and uses supported `HTML_HEADER`, `HTML_EXTRA_FILES`, and `HTML_EXTRA_STYLESHEET` settings. `--source-root` changes only the source input; configuration, branding, CSS, JavaScript, and header customization always come from the tooling checkout. UTF-8 is the default. When legacy C# files are detected, the supported `INPUT_FILTER` mechanism runs `source_filter.py`: valid UTF-8 is passed through unchanged and Windows-1252 is strictly transcoded to UTF-8 on stdout, including source listings. Source snapshots are never rewritten. This avoids Doxygen's lowercasing of `INPUT_FILE_ENCODING` patterns, which fails to match mixed-case paths on case-sensitive Linux. Every build validates all generated HTML as strict UTF-8 before any publishing; failures report the exact page and byte offset. Generated output must be empty and outside the source directory. No Doxygen fork or third-party UI library is required. See [Doxygen configuration](https://www.doxygen.nl/manual/config.html).

`publish.sh` fetches the latest `gh-pages`, applies `publish.py` to a disposable worktree, and performs a normal fast-forward push. On a concurrent update it fetches again and reapplies the scoped update, up to five times. Normal publishing never replaces the whole branch. Development and release documentation share the `api-docs-publication` concurrency group. The workflows explicitly request a Pages build after pushing because a `GITHUB_TOKEN` push does not itself trigger one. Pages must be configured to deploy from `gh-pages`, `/`.

The API publisher owns only:

| Path | Policy |
| --- | --- |
| `api/latest/` | Replace completely on development publication, including removing stale development files |
| `api/vX.Y.Z/` | Create once; leave existing snapshots byte-for-byte unchanged on every rerun |
| `api/versions.json` | Rebuild from existing canonical release directories with an `index.html`; unique, numerically sorted newest-first |
| `api/index.html` | Release publication only; redirect to the highest published release, preventing old reruns from downgrading stable |

Normal publishing preserves everything outside these paths, including the root compatibility redirect and `.nojekyll`. Release snapshots load the shared manifest without caching so newly published versions appear in older selectors without changing their files. URLs are relative to the copied script's location, preserving the `/Gondwana/` Pages base path even on deep pages. The GitHub Wiki is a separate repository and is never touched.

## Historical archive migration

The one-time API history migration is complete and its executable migration tooling has been retired. The historical archive covers v2.1.0 through v2.5.2: 2.1.0–2.1.2, 2.2.0–2.2.4, 2.3.0, 2.4.0–2.4.3, and 2.5.0–2.5.2. The old root site's v2.5.3 label was unreleased development for v2.6.0, not a release, and was not archived. v2.0.1 was intentionally excluded.

The migration regenerated historical documentation from exact tagged source snapshots while applying the current documentation configuration uniformly. It did not alter historical tags, releases, packages, changelogs, or source. The retired `rebuild.py` and its dedicated rebuild tests were removed after successful live verification. The one-time workflow was previously retired as well.

The migration's validation record is intentionally retained in [history-validation.md](history-validation.md). The recovery branch `gh-pages-pre-versioning-20260908` preserves the pre-versioning Pages state and should remain available as historical rollback evidence. Backup refs created during the actual migration should likewise be retained.

### Emergency rollback reference

A rollback should only be considered if the versioned Pages archive itself must be restored to its pre-migration state. First pause Docs and release-documentation publishers and allow active runs to finish. Identify the backup ref associated with the migration and the currently published `gh-pages` commit before making any change. Use a lease-protected update; if the lease fails, inspect the concurrent change instead of weakening the protection.

```bash
backup_ref=refs/heads/gh-pages-pre-versioning-YYYYMMDD-suffix
published_sha=REPLACE_WITH_CURRENT_REVIEWED_GH_PAGES_COMMIT
git fetch origin "$backup_ref"
backup_sha=$(git rev-parse FETCH_HEAD)
git ls-remote --heads origin gh-pages "$backup_ref"
git push --force-with-lease="refs/heads/gh-pages:$published_sha" origin "$backup_sha:refs/heads/gh-pages"
gh api --method POST repos/Isthimius/Gondwana/pages/builds
```

Verify the Pages build and restored site before resuming normal publishers. The backup ref should remain intact.

When v2.6.0 and later stable versions are released, the ordinary release job creates `/api/vX.Y.Z/`, updates the shared manifest and `/api/index.html`, and leaves all older directories unchanged. Development regeneration replaces only `api/latest/` and updates development availability in the shared manifest. The root continues to redirect to `api/`. Snapshots are retained indefinitely; there is no pruning policy.

## Local validation

Requires Python 3, Node.js, and Doxygen 1.14.0 for the build smoke test. On Linux x64, run `bash Tooling/scripts/api-docs/install-doxygen.sh` and apply the printed PATH export. The Python suite includes a real-Doxygen regression for mixed UTF-8/Windows-1252 sources, mixed-case paths, duplicate filenames and spaces; it is skipped if Doxygen is absent, so CI installs Doxygen before running the suite.

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
