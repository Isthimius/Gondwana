# Historical API archive validation — 2026-09-08

This records local staging validation, **not a completed live migration**. No generated HTML is committed to master, and `gh-pages` was not replaced during implementation.

## Provenance and recovery

- Inspected master: `4fa9945675bdd614b323925fdecaf3d950fb276b` (NBGV 2.6.0).
- Original Pages commit: `b67c960918d46858a1c5f166350322060dede084`.
- Created and verified recovery ref: `refs/heads/gh-pages-pre-versioning-20260908`, pointing to that exact Pages commit. Retain it after publication.
- All 16 exact release source snapshots built with current Doxygen configuration, current header customization, selector JS/CSS, and Doxygen 1.14.0 on Windows. Development built last from the recorded master SHA. The manually dispatched Ubuntu workflow revalidates everything using its installed Doxygen before publishing.

| Release | Source commit | Doxygen warnings |
| --- | --- | ---: |
| v2.1.0 | 5933a80c658ec469cbb084d1e8cc463d6fedaed3 | 3 |
| v2.1.1 | 039c7ea01f21357697361cb296f4c68538cc79ba | 3 |
| v2.1.2 | 6defdbce5c9b0a4491fbf735da1e207a03100daa | 3 |
| v2.2.0 | 0ccb5bcf73d6359c9040a29754079325fa790e47 | 3 |
| v2.2.1 | 142436e0d595b99fa4a3a1ba9804d8eaebf8b277 | 3 |
| v2.2.2 | ea663b7cbac4b242c06bb083b639f174acb85fd0 | 3 |
| v2.2.3 | 9c9e70377a987db2ef3bd594c9d630fc760f27ab | 3 |
| v2.2.4 | bb033b0ded6a10f8fb4ffe5171b9ff106e729237 | 3 |
| v2.3.0 | f85ad5b533096af6466a311e037babb651d11fb0 | 3 |
| v2.4.0 | 6e85d0f6c23e43eeae9bd292de67af48c6b80860 | 3 |
| v2.4.1 | 490f1ffd6b115f11b104bfa93101d90537e2dd40 | 3 |
| v2.4.2 | 3707e98e7acdd96cd7d8d1fac2e98ee3f83850bf | 3 |
| v2.4.3 | 8c578ba3d534c501a157748e63f513e0240222db | 3 |
| v2.5.0 | 9da4631dd46c41421cc4d3e2c06c6e00c2f82700 | 3 |
| v2.5.1 | a27f31465bb7681e61e3bdf8bf50ee022831536d | 4 |
| v2.5.2 | 610916e2b72aa57f20066484f0cfa04fc6687992 | 4 |
| Development (master) | 4fa9945675bdd614b323925fdecaf3d950fb276b | 4 |

## Site validation

- Final staged bytes: **297,251,880** (**283.4815 MiB**).
- File count: **20,731**. Largest file: **339,674 bytes** (`latest/doxygen_crawl.html`).
- All 17 directories contain their home, representative class/member pages, current selector assets, and source provenance. Manifest contains exactly 16 releases, semantically ordered newest-first, stable v2.5.2, development available.
- No v2.0.1 or fictional v2.5.3 directory; no v2.5.3 release label in any generated page. Root is only `.nojekyll`, automatic `index.html`, and `api/`.
- Chromium tests against the real generated site under `/Gondwana/` passed for all release/development homes and member pages. Both automatic redirects work with and without JavaScript. Switching among v2.1.0, v2.3.0, v2.5.2, and development preserves a common class and its member anchor. Missing pages fall back to the selected API home. Existing fixture tests also cover nested Doxygen paths and development's direct stable link.
- Python publisher and migration tests cover immutable releases, development replacement, semantic sorting, redirects, tag validation, source configuration, encoding, size limits, unreviewed content, and confirmation. A real temporary bare Git remote verifies backup creation/conflict handling, concurrent-update rejection (including a race after preflight), parentless staged publication, and backup retention.
- Shell, Python, JavaScript, and workflow YAML syntax checked. No engine code changed; validation targets the API publisher rather than .NET runtime behavior.

## Parser diagnostics

Doxygen exits successfully for every version. Existing warnings are distinct from generation failures:

- `RenderSurfaceHost.cs`: initializer-list parser warning about a semicolon (line 55 in oldest source, line 99 in current source).
- `TextBlock.cs`: unknown `\S` and `\s` commands in comments.
- v2.5.1 onward: an undocumented `SplashScreen.TryCreate` parameter.

The initial build exposed Windows-1252 punctuation copied into 42 HTML pages from old `TextBlock.cs`/`Scene.cs` text through v2.5.0. The permanent builder now tells Doxygen the encoding of non-UTF-8 C# inputs without rewriting them. The full rebuild has **zero invalid UTF-8 HTML pages**. Other detected legacy C# inputs (including Sprite and an excluded demo) receive the same decoding rule. No source parsing failure remains that blocks staging validation.

Hosted deployment duration and live-site checks remain pending manual dispatch. The workflow verifies branch-based Pages settings before publication, enforces the 750 MiB and per-file limits, explicitly requests a Pages build, and waits up to ten minutes for the published commit. Review its logs if the Ubuntu Doxygen version reports different warnings. Follow the [dispatch and recovery guide](README.md#review-dispatch-verification-and-recovery).
