# Gondwana Animations (WinForms)

Standalone .NET 8 Windows editor for Gondwana `.gani` animation definitions.

This project intentionally separates the reusable `AnimationEditorControl` from the
standalone `MainForm`. The application hosts the editor control in DockPanelSuite;
a future Gondwana Studio host can reuse the editor surface without depending on the
standalone application shell.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Animations.WinForms -c Release
```

The editor implementation is being built around the GANI definition model rather
than runtime `Animator` state.
