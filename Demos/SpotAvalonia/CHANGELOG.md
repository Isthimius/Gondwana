# Changelog

All notable changes to this project will be documented in this file.

# [Unreleased]



## Added
- Update SpotAvalonia to use BrowserAudioManager for WASM audio (PR 4)
- Add support for .gts files and tilesheets ([#209](https://github.com/Isthimius/Gondwana/pull/209))
- Move SplashScreen and add Gondwana.Widgets ([#225](https://github.com/Isthimius/Gondwana/pull/225))
- Add base classes for draggable widgets ([#226](https://github.com/Isthimius/Gondwana/pull/226))
- Implement widget input handling and routing ([#240](https://github.com/Isthimius/Gondwana/pull/240))



## Fixed
- Add missing using for ColorItem in Player.cs
- Dispose _particleSurface before overwriting in AddClouds()
- Use SizeToContent.Height in NewGameDialog so buttons are visible
- Address PR review feedback (App.cs BROWSER guard, duplicate JS, PS5.1 $IsWindows, dead code, doc fix)
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm ([#212](https://github.com/Isthimius/Gondwana/pull/212))
- Restore SpotAvalonia desktop splash and about dialog ([#216](https://github.com/Isthimius/Gondwana/pull/216))
- Remove obsolete SpotAvalonia files ([#218](https://github.com/Isthimius/Gondwana/pull/218))



## Refactoring
- Improve Tilesheet and region handling ([#186](https://github.com/Isthimius/Gondwana/pull/186))



## Other Changes
- Add SpotAvalonia WASM demo using Avalonia adapters and timer-driven engine start
- Fix review feedback: redundant else-if, inconsistent Random usage, missing PlayerMoveStarted in Dispose
- Fix SpotAvalonia: show splash screen and player selection before starting game
- Extract shared colors and board sizes to GameConfig
- Clarify comments per code review feedback
- Capture IsChecked on UI thread before posting to engine dispatcher
- BrowserExe
- Add Avalonia Fluent theme so menu bar renders in SpotAvalonia
- Adding Avalonia.Themes.Fluent
- Cleaning dup ref
- Persist new game dialog selections between games
- Add clarifying comments for combo box offset constants
