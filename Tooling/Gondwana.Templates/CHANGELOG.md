# Changelog

All notable changes to this project will be documented in this file.


# v2.4.0 - June 09, 2026



## Other Changes
- Align generated GameHost filename with derived host class name




# v2.3.0 - May 20, 2026



## Added
- Add Gondwana.Templates dotnet new template package
- Add gondwana-avalonia template and CLI command
- Add --backbuffer option to gondwana new winforms/avalonia commands
- Add gondwana-wasm template, gondwana new wasm CLI command, docs (PR 2)
- Wire touch adapter into GameHost lifecycle same as keyboard and mouse



## Fixed
- Use 2.* package versions in template and add LogLevel tip comment
- Remove redundant conditional using directives in template GameHost/GameWindow files
- Address PR review feedback (App.cs BROWSER guard, duplicate JS, PS5.1 $IsWindows, dead code, doc fix)
- Update stale first-game-in-15-minutes.md links across all templates and Gondwana.Templates README



## Documentation
- Replace first-game-in-15-minutes.md with wiki page; add CLI method tutorial



## Maintenance
- Add dev install scripts for Gondwana.Cli and Gondwana.Templates



