# Changelog

All notable changes to this project will be documented in this file.

# [Unreleased]

## Changed
- Rebuilt SpotAvalonia from the current canonical Spot implementation.
- Replaced the drifted bitmap host with the Avalonia GPU host/render surface.
- Reused the shared Spot game model, Widget UI, gameplay, HUD, particles, and settings.
- Removed the duplicated Avalonia-specific game model and native dialog/menu implementation.
- Added Avalonia keyboard translation for shared Widget text input.
- Kept desktop audio optional when no compatible cross-platform backend is configured.

# v2.6.0 - September 17, 2026
