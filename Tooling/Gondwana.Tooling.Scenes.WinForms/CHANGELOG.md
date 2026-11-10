# Changelog

All notable changes to Gondwana.Tooling.Scenes.WinForms are documented here.

## [Unreleased]

### Added
- Standalone and hostable WinForms editor for GSCN scene definitions.

# [Unreleased]

# v2.6.2 - November 03, 2026



## Added
- Support multiple definitions per asset file ([#452](https://github.com/Isthimius/Gondwana/pull/452))
- Persist sprite animation assignments and preserve Cycle clone flags ([#453](https://github.com/Isthimius/Gondwana/pull/453))
- Support portable reference relocation and atomic persistence ([#455](https://github.com/Isthimius/Gondwana/pull/455))
- Restore saved authoring workspace sessions ([#481](https://github.com/Isthimius/Gondwana/pull/481))
- Open, edit, and safely save packed definitions (Step 3) ([#485](https://github.com/Isthimius/Gondwana/pull/485))
- Launch packed scenes from temporary snapshots ([#490](https://github.com/Isthimius/Gondwana/pull/490))
- Add preview-only per-layer visibility controls ([#495](https://github.com/Isthimius/Gondwana/pull/495))
- Embed gondwana branding in winforms tools ([#500](https://github.com/Isthimius/Gondwana/pull/500))
- Add shared selectable themes to editors ([#505](https://github.com/Isthimius/Gondwana/pull/505))
- Add global command toolbar with Save All and validation ([#507](https://github.com/Isthimius/Gondwana/pull/507))
- Complete GAF dependency pickers across native editors ([#514](https://github.com/Isthimius/Gondwana/pull/514))
- Add dependency explorer and cross-document navigation ([#515](https://github.com/Isthimius/Gondwana/pull/515))
- Add shared undo/redo foundation for GTS and GSCN ([#519](https://github.com/Isthimius/Gondwana/pull/519))
- Extend shared undo/redo to GANI, GSPR, and GSND ([#521](https://github.com/Isthimius/Gondwana/pull/521))
- Add select, paint and erase tool modes ([#527](https://github.com/Isthimius/Gondwana/pull/527))
- Add drag painting and multi-cell selection ([#528](https://github.com/Isthimius/Gondwana/pull/528))
- Complete visual editing with rectangle, fill and bulk tools ([#529](https://github.com/Isthimius/Gondwana/pull/529))
- Order inspector fields and improve composite editors ([#539](https://github.com/Isthimius/Gondwana/pull/539))



## Fixed
- Resolve GANI preview tilesheets by member identity ([#454](https://github.com/Isthimius/Gondwana/pull/454))
- Gscn initialize tile frame from assigned animation ([#482](https://github.com/Isthimius/Gondwana/pull/482))
- Resolve packed assets in authoring previews ([#483](https://github.com/Isthimius/Gondwana/pull/483))
- Stabilize packed authentication and integrations ([#487](https://github.com/Isthimius/Gondwana/pull/487))
- Start playback after assigning animations ([#497](https://github.com/Isthimius/Gondwana/pull/497))
- Sync zoom selectors with preview magnification ([#502](https://github.com/Isthimius/Gondwana/pull/502))
- Add property-grid help descriptions ([#504](https://github.com/Isthimius/Gondwana/pull/504))
- Eliminate duplicated preview overlay text during wheel scrolling ([#517](https://github.com/Isthimius/Gondwana/pull/517))
- Correct toolbar dpi scaling and glyph resolution ([#530](https://github.com/Isthimius/Gondwana/pull/530))



## Documentation
- Clarify XML documentation across Gondwana tooling ([#491](https://github.com/Isthimius/Gondwana/pull/491))

# v2.6.1 - October 08, 2026



## Added
- Native scene tile transforms, GSCN tooling and Tiled import ([#417](https://github.com/Isthimius/Gondwana/pull/417))



## Maintenance
- Add formatting and analyzer enforcement ([#398](https://github.com/Isthimius/Gondwana/pull/398))



## Other Changes
- Add xml comments to public and protected memebers
