# PlantUML diagrams in the Gondwana Wiki

The authoritative diagrams live in `Solution Items/documentation/*.puml`. Generated assets live in `docs/wiki/diagrams/*.svg`. Archived `old/` diagrams are excluded.

## Automatic regeneration

`.github/workflows/plantuml-wiki-diagrams.yml` listens only for **pushes changing a .puml source** under `Solution Items/documentation/`.

- Feature branches: render all active diagrams, validate output, and commit changed SVGs to the same branch using the existing `WIKI_SYNC_TOKEN` credential for the push step. A PAT-authored push triggers standard PR CI; GitHub's default workflow token would not.
- `master`: validate that committed SVGs match the source instead of pushing to a protected branch.
- Release tags, archived diagrams, unrelated commits, regular wiki edits, and SVG-only commits: **do not run** this workflow.

After merge, the existing `wiki-sync.yml` workflow publishes `docs/wiki/` unchanged. The SVG push step **reuses** the `WIKI_SYNC_TOKEN` repository secret already required by wiki-sync. It must have repository Contents write permission and must be a PAT (or equivalent GitHub App token) so GitHub recognizes the new commit as a normal event. No extra secret is required. The credential is exposed only to the final push step, not to PlantUML or the renderer.

PlantUML is pinned to **1.2026.8** and verified using SHA-256. The runner provides Java 21. PlantUML uses its integrated Smetana fallback if Graphviz is unavailable.

## Local rendering

Install Java and Graphviz, download the pinned JAR, then run:

```bash
python3 Tooling/scripts/plantuml-wiki/render.py --jar /path/to/plantuml-1.2026.8.jar
```

Edit the `.puml` source, never the SVG. Pull requests from forks cannot push generated assets to the upstream repository; a maintainer must render on a same-repository branch or commit images manually.
