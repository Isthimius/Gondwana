# Gondwana Scenes (WinForms)

Standalone .NET 8 Windows authoring tool for Gondwana `.gscn` scene definitions.

This project is intentionally definition-driven. `SceneEditorControl` edits a
`SceneDefinition` without booting a `GameHost`. The standalone executable is a
host for the same reusable WinForms control that Gondwana Studio can embed later.

This initial scaffold establishes editor-local docking and GSCN authoring-source
metadata. Full scene preview, GTS frame selection, GANI assignment, tile editing,
working-directory hosting, tests, and documentation are being completed in this PR.
