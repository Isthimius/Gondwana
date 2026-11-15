# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Browse, select, preview, and validate images stored in Gondwana `.gaf` / `.zip` asset packages without extracting them.
- Create GTS definitions directly from packed image entries and persist logical GAF references.

### Changed
- Refactor the tilesheet editor and project-source browser into hostable WinForms controls for future Gondwana Studio composition.
- Keep DockPanelSuite document/window behavior in thin standalone-app wrappers.


# [Unreleased]

# v2.6.2 - November 03, 2026



## Added
- Support multiple definitions per asset file ([#452](https://github.com/Isthimius/Gondwana/pull/452))
- Support portable reference relocation and atomic persistence ([#455](https://github.com/Isthimius/Gondwana/pull/455))
- Restore saved authoring workspace sessions ([#481](https://github.com/Isthimius/Gondwana/pull/481))
- Open, edit, and safely save packed definitions (Step 3) ([#485](https://github.com/Isthimius/Gondwana/pull/485))
- Embed gondwana branding in winforms tools ([#500](https://github.com/Isthimius/Gondwana/pull/500))
- Add shared selectable themes to editors ([#505](https://github.com/Isthimius/Gondwana/pull/505))
- Add dependency explorer and cross-document navigation ([#515](https://github.com/Isthimius/Gondwana/pull/515))
- Add shared undo/redo foundation for GTS and GSCN ([#519](https://github.com/Isthimius/Gondwana/pull/519))
- Group gts region geometry in property inspector ([#520](https://github.com/Isthimius/Gondwana/pull/520))
- Extend shared undo/redo to GANI, GSPR, and GSND ([#521](https://github.com/Isthimius/Gondwana/pull/521))
- Add image metadata and mask color editing ([#538](https://github.com/Isthimius/Gondwana/pull/538))
- Order inspector fields and improve composite editors ([#539](https://github.com/Isthimius/Gondwana/pull/539))



## Fixed
- Resolve packed assets in authoring previews ([#483](https://github.com/Isthimius/Gondwana/pull/483))
- Stabilize packed authentication and integrations ([#487](https://github.com/Isthimius/Gondwana/pull/487))
- Sync zoom selectors with preview magnification ([#502](https://github.com/Isthimius/Gondwana/pull/502))
- Add property-grid help descriptions ([#504](https://github.com/Isthimius/Gondwana/pull/504))
- Correct toolbar dpi scaling and glyph resolution ([#530](https://github.com/Isthimius/Gondwana/pull/530))



## Refactoring
- Group scene, audio, and sprite properties ([#510](https://github.com/Isthimius/Gondwana/pull/510))



## Documentation
- Clarify XML documentation across Gondwana tooling ([#491](https://github.com/Isthimius/Gondwana/pull/491))

# v2.6.1 - October 08, 2026



## Added
- Add editor-local docking to GTS, GAF, and GANI ([#378](https://github.com/Isthimius/Gondwana/pull/378))
- Compose the standalone WinForms authoring editors ([#383](https://github.com/Isthimius/Gondwana/pull/383))



## Fixed
- Restore closed panes in standalone editors ([#382](https://github.com/Isthimius/Gondwana/pull/382))



## Maintenance
- Add formatting and analyzer enforcement ([#398](https://github.com/Isthimius/Gondwana/pull/398))



## Other Changes
- Add xml comments to public and protected memebers

# v2.6.0 - September 17, 2026



## Added
- Add standalone gts editor ([#357](https://github.com/Isthimius/Gondwana/pull/357))
- Add image resize and border controls ([#358](https://github.com/Isthimius/Gondwana/pull/358))



## Maintenance
- Rename tooling projects and files ([#262](https://github.com/Isthimius/Gondwana/pull/262))
