# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

- Support the optional Project Diagnostics reference plugin, including real discovery, lifecycle and docking integration tests; share the WinForms plugin contract with external load contexts.

- Host the real GAF/GTS/GANI/GSND/GSCN editor controls as individual Studio documents with editor-local docking.
- Add combined file commands, lazy working-directory browsing, dirty/close handling, Save As path tracking, and View pane recovery.
- Remove duplicate Studio editors and the deprecated Avalonia Studio prototype; retain shell/plugin infrastructure.
- Add Windows composition tests covering nested ownership, saving, cancellation, encrypted assets, and disposal.

# [Unreleased]



## Added
- Compose the standalone WinForms authoring editors ([#383](https://github.com/Isthimius/Gondwana/pull/383))
- Add external asset import pipeline for tmx/tsx, Godot assets, Aesprite ([#390](https://github.com/Isthimius/Gondwana/pull/390))



## Maintenance
- Add formatting and analyzer enforcement ([#398](https://github.com/Isthimius/Gondwana/pull/398))

# v2.6.0 - September 17, 2026



## Maintenance
- Rename tooling projects and files ([#262](https://github.com/Isthimius/Gondwana/pull/262))
