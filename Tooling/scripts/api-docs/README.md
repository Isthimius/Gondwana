# Versioned API documentation

- [Stable API](https://isthimius.github.io/Gondwana/api/) (`/api/`) redirects to the highest published stable release.
- [Development API](https://isthimius.github.io/Gondwana/api/latest/) (`/api/latest/`) documents current `master`, explicitly marked development/unreleased.
- `/api/vX.Y.Z/` is an immutable release snapshot. Use the header's **API version** selector for historical releases, current stable, and development.

The header displays the injected Doxygen `PROJECT_NUMBER`: `Version X.Y.Z` for a release or `Development (master) - <NBGV version>` for development. On development pages, the header also links directly to the latest stable release. Switching versions attempts the same relative HTML page and preserves its query and member anchor; a missing page or failed lookup falls back to the selected version's home. Member anchors can change between releases; if only the anchor disappears, the corresponding class page still opens.

## Build and publication

`docs.yml` runs for relevant `master` pushes and manual dispatch on `master`. It checks out current `master` explicitly. C# sources, project files, MSBuild props/targets, version/SDK configuration, Doxygen assets, and API publishing scripts trigger it. Tests, demos, tools, and the other Doxygen-excluded source trees do not; README/changelog/wiki-only changes do not. These exclusions mirror `EXCLUDE_PATTERNS` in the Doxyfile. If Doxygen inputs expand to another language or source tree, update the trigger filters with them.

The release workflow's separate `api-docs` job runs after successful release publication, checks out the triggering tag, and accepts only canonical stable `vX.Y.Z` tags. It injects the tag version rather than guessing from NBGV. Failed documentation jobs can be retried independently of successful packaging jobs.

`build.py` generates the installed Doxygen's default header with `doxygen -w html`, adds the version navigation, and uses supported `HTML_HEADER`, `HTML_EXTRA_FILES`, and `HTML_EXTRA_STYLESHEET` settings. Generated output stays outside source directories. No Doxygen fork or third-party UI library is required. See [Doxygen configuration](https://www.doxygen.nl/manual/config.html).

`publish.sh` fetches the latest `gh-pages`, applies `publish.py` to a disposable worktree, and performs a normal fast-forward push. On a concurrent update it fetches again and reapplies the scoped update, up to five times. There are no force pushes or whole-branch replacement. Other Pages publishers must likewise preserve API paths and avoid destructive force pushes.

The API publisher owns only:

| Path | Policy |
| --- | --- |
| `api/latest/` | Replace completely on development publication, including removing stale development files |
| `api/vX.Y.Z/` | Create once; leave existing snapshots byte-for-byte unchanged on every rerun |
| `api/versions.json` | Rebuild from existing canonical release directories with an `index.html`; unique, numerically sorted newest-first |
| `api/index.html` | Release publication only; redirect to the highest published release, preventing old reruns from downgrading stable |

Everything else in `gh-pages`, including wiki content and legacy documentation at the branch root, remains intact. Release snapshots load the shared manifest without caching so newly published versions appear in older selectors without changing their files. URLs are relative to the copied script's location, preserving the `/Gondwana/` Pages base path even on deep pages.

## Initial rollout and retention

The first development publish does not fabricate stable documentation from `master`. Until the first release snapshot is published, development says no stable API snapshot is available; `/api/` starts working when that release is published. Existing unversioned root documentation remains accessible at its original URLs. No existing output is relabeled as a historical release, since its source provenance is unknown.

Earlier tags are not automatically backfilled by merging this change, and their old workflows do not gain this implementation. To backfill an earlier release, generate from a separate checkout of that exact tag using the current header/publishing tooling, inspect the output, and publish with that canonical tag. Do not rerun an older destructive docs workflow. An existing immutable snapshot cannot be regenerated in place: intentional replacement or removal requires manual maintenance. Otherwise snapshots are retained indefinitely; there is no pruning policy.

## Local validation

Requires Python 3, Node.js, and Doxygen for the build smoke test:

```sh
python3 -m unittest discover -s Tooling/scripts/api-docs -p 'test_*.py' -v
node --check docs/doxy/api-versions.js
# With Playwright and Chromium installed:
node Tooling/scripts/api-docs/test-selector.cjs
bash -n Tooling/scripts/api-docs/publish.sh
python3 Tooling/scripts/api-docs/build.py --version 'Development (master) - 2.6.0-test' --output /tmp/gondwana-api-build
```

`publish.py --site <test-site> --source <build>/html --version latest` can simulate publication without Git or network access. Repeat with two release tags to verify retention and the stable redirect. Serve the test site under `/Gondwana/` over HTTP to test manifest loading and deep-page switching (opening files with `file://` does not support fetch).
