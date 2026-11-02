# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

- Support the optional Project Diagnostics reference plugin, including real discovery, lifecycle and docking integration tests; share the WinForms plugin contract with external load contexts.

- Host the real GAF/GTS/GANI/GSND/GSCN editor controls as individual Studio documents with editor-local docking.
- Add combined file commands, lazy working-directory browsing, dirty/close handling, Save As path tracking, and View pane recovery.
- Remove duplicate Studio editors and the deprecated Avalonia Studio prototype; retain shell/plugin infrastructure.
- Add Windows composition tests covering nested ownership, saving, cancellation, encrypted assets, and disposal.

# v2.6.2-beta.1 - November 02, 2026



## Added
- Add reference-aware asset moves and renames ([#460](https://github.com/Isthimius/Gondwana/pull/460))
- Add reference-aware asset deletion ([#461](https://github.com/Isthimius/Gondwana/pull/461))
- Group working-directory files and add context menus ([#477](https://github.com/Isthimius/Gondwana/pull/477))
- Display packed assets in Working Directory (Step 1) ([#480](https://github.com/Isthimius/Gondwana/pull/480))
- Restore saved authoring workspace sessions ([#481](https://github.com/Isthimius/Gondwana/pull/481))
- Open, edit, and safely save packed definitions (Step 3) ([#485](https://github.com/Isthimius/Gondwana/pull/485))
- Improve document and workspace session handling ([#489](https://github.com/Isthimius/Gondwana/pull/489))
- Group packed assets and persist icon sizing ([#493](https://github.com/Isthimius/Gondwana/pull/493))
- Show file-type icons on document tabs ([#494](https://github.com/Isthimius/Gondwana/pull/494))
- Select active document in working directory tree ([#496](https://github.com/Isthimius/Gondwana/pull/496))
- Embed gondwana branding in winforms tools ([#500](https://github.com/Isthimius/Gondwana/pull/500))
- Add shared selectable themes to editors ([#505](https://github.com/Isthimius/Gondwana/pull/505))
- Add global command toolbar with Save All and validation ([#507](https://github.com/Isthimius/Gondwana/pull/507))
- Complete GAF dependency pickers across native editors ([#514](https://github.com/Isthimius/Gondwana/pull/514))
- Add dependency explorer and cross-document navigation ([#515](https://github.com/Isthimius/Gondwana/pull/515))
- Add type-folder document creation ([#518](https://github.com/Isthimius/Gondwana/pull/518))
- Add shared undo/redo foundation for GTS and GSCN ([#519](https://github.com/Isthimius/Gondwana/pull/519))
- Extend shared undo/redo to GANI, GSPR, and GSND ([#521](https://github.com/Isthimius/Gondwana/pull/521))
- Color output lines by severity ([#523](https://github.com/Isthimius/Gondwana/pull/523))
- Complete shared undo/redo with GAF history ([#524](https://github.com/Isthimius/Gondwana/pull/524))



## Fixed
- Resolve packed assets in authoring previews ([#483](https://github.com/Isthimius/Gondwana/pull/483))
- Stabilize packed authentication and integrations ([#487](https://github.com/Isthimius/Gondwana/pull/487))
- 48px toolbar buttons with hover tooltips ([#508](https://github.com/Isthimius/Gondwana/pull/508))
- Make dependency query scope an explicit reversible toggle ([#516](https://github.com/Isthimius/Gondwana/pull/516))
- Size dependency summary for wrapped text ([#522](https://github.com/Isthimius/Gondwana/pull/522))
- Correct toolbar dpi scaling and glyph resolution ([#530](https://github.com/Isthimius/Gondwana/pull/530))



## Documentation
- Clarify XML documentation across Gondwana tooling ([#491](https://github.com/Isthimius/Gondwana/pull/491))

# v2.6.1 - October 08, 2026



## Added
- Compose the standalone WinForms authoring editors ([#383](https://github.com/Isthimius/Gondwana/pull/383))
- Add external asset import pipeline for tmx/tsx, Godot assets, Aesprite ([#390](https://github.com/Isthimius/Gondwana/pull/390))
- Persist and restore the project folder within Gondwana Studio ([#415](https://github.com/Isthimius/Gondwana/pull/415))
- Support Godot 3 TileSet resources ([#416](https://github.com/Isthimius/Gondwana/pull/416))



## Documentation
- Credit DockPanel Suite creator ([#412](https://github.com/Isthimius/Gondwana/pull/412))



## Maintenance
- Add formatting and analyzer enforcement ([#398](https://github.com/Isthimius/Gondwana/pull/398))



## Other Changes
- Add xml comments to public and protected memebers

# v2.6.0 - September 17, 2026



## Maintenance
- Rename tooling projects and files ([#262](https://github.com/Isthimius/Gondwana/pull/262))
