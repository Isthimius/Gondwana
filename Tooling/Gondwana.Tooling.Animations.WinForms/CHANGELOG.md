# Changelog

## [Unreleased]

### Added

- Standalone dark-themed DockPanelSuite GANI editor.
- Hostable `AnimationEditorControl` separated from the application shell.
- New/open/save/save-as and dirty-document workflows.
- Multi-GTS source browser with lazy region/row/frame expansion.
- GANI frame append, remove and reorder operations.
- Animation property editing for key, timing, cycle behavior and next-cycle identity.
- Live nearest-neighbor animation preview with play, restart, step and zoom controls.
- Structural validation plus loaded-GTS resolution warnings.

# [Unreleased]



## Added
- Support multiple definitions per asset file ([#452](https://github.com/Isthimius/Gondwana/pull/452))
- Support portable reference relocation and atomic persistence ([#455](https://github.com/Isthimius/Gondwana/pull/455))
- Restore saved authoring workspace sessions ([#481](https://github.com/Isthimius/Gondwana/pull/481))
- Open, edit, and safely save packed definitions (Step 3) ([#485](https://github.com/Isthimius/Gondwana/pull/485))
- Show file-type icons on document tabs ([#494](https://github.com/Isthimius/Gondwana/pull/494))
- Show source-sized animation frame thumbnails ([#499](https://github.com/Isthimius/Gondwana/pull/499))
- Embed gondwana branding in winforms tools ([#500](https://github.com/Isthimius/Gondwana/pull/500))



## Fixed
- Resolve packed assets in authoring previews ([#483](https://github.com/Isthimius/Gondwana/pull/483))
- Stabilize packed authentication and integrations ([#487](https://github.com/Isthimius/Gondwana/pull/487))



## Documentation
- Clarify XML documentation across Gondwana tooling ([#491](https://github.com/Isthimius/Gondwana/pull/491))

# v2.6.1 - October 08, 2026



## Added
- Add editor-local docking to GTS, GAF, and GANI ([#378](https://github.com/Isthimius/Gondwana/pull/378))
- Add external asset import pipeline for tmx/tsx, Godot assets, Aesprite ([#390](https://github.com/Isthimius/Gondwana/pull/390))



## Fixed
- Restore closed panes in standalone editors ([#382](https://github.com/Isthimius/Gondwana/pull/382))



## Tests
- Cover repeated GTS source selection ([#377](https://github.com/Isthimius/Gondwana/pull/377))



## Maintenance
- Add formatting and analyzer enforcement ([#398](https://github.com/Isthimius/Gondwana/pull/398))



## Other Changes
- Add xml comments to public and protected memebers
