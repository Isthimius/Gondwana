# Doxygen configuration and assets

This directory holds the source configuration and presentation assets for
[Gondwana's generated API reference](https://isthimius.github.io/Gondwana/api/).
It is not the generated HTML documentation.

| File | Purpose |
| --- | --- |
| `Gondwana_doxy` | Doxygen configuration template for the engine API |
| `Gondwana.png` | Logo displayed in generated API documentation |
| `api-versions.js` | Version selector and links between stable, historical, and development API pages |
| `api-versions.css` | Styling for the version selector |

## How these files are used

[`Tooling/scripts/api-docs/build.py`](../../Tooling/scripts/api-docs/build.py)
reads `Gondwana_doxy`, substitutes its `@PROJECT_VERSION@` placeholder,
and supplies the input and output directories. It generates a Doxygen header
with version navigation and copies the branding and selector assets into
the resulting HTML documentation.

The [Docs workflow](../../.github/workflows/docs.yml) builds the development
reference at `/api/latest/`. Release publishing creates versioned snapshots
at `/api/vX.Y.Z/`, while `/api/` points to the latest stable release.
The API publisher maintains the version manifest consumed by
`api-versions.js`.

For build commands, tests, publishing, retention policies, and recovery
procedures, see the
[Versioned API documentation guide](../../Tooling/scripts/api-docs/README.md).

Do not commit generated HTML here; modify the configuration or assets and
let the existing build/publishing pipeline generate the site.
