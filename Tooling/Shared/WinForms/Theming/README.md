# Shared WinForms tooling themes

Studio and the six standalone editors (Assets, Tilesheets, Animations, Audio,
Scenes, and Sprites) provide **View → Theme → Dark / Dark Blue / Light**.
Dark is the default. Dark Blue uses navy/charcoal surfaces and subdued blue
selections. Light uses coordinated light surfaces and dark text. Preview pixels,
transparency checkerboards, overlay colors, and asset-type icons retain their own colors.

The checkmark identifies the **saved selection for the next launch**. Selecting a
theme saves it immediately and displays a restart notification. Save your work and
close/reopen the application normally. Other running tools keep their appearance;
newly launched tools use the shared selection. No document is closed, recreated,
marked dirty, or saved by theme selection. Selecting the currently running theme
cancels a pending appearance change for the next launch.

## Preference storage

`%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/theme.json` contains the
shared per-user choice (`Dark`, `DarkBlue`, or `Light`). It is outside application,
project, layout, session, directory and icon-size settings. Missing, invalid,
unsupported or corrupt values fall back to Dark. Writes use a temporary file and
atomic replacement; a failed write displays an error and keeps the previous choice.
The existing `Gondwana.Tooling.SettingsRoot` AppContext override redirects the
shared preference for embedding hosts and isolated tests. ApplicationId does not
partition theme preferences.

## Implementation and extensions

`Gondwana.Tooling.WinForms.Theming` is a small Windows-only shared library, with
no editor or runtime dependencies. All seven applications reference the same
assembly, so Studio and embedded editors use one immutable process-wide theme.
`ToolingPalette` owns semantic colors; add future themes there and to the enum
and docking factory. Menus are generated in enum order by `CreateThemeMenu`.

Call `ToolingAppearance.Apply(root)` once after constructing a control tree.
The helper tracks controls weakly, watches added children and context menus, and
styles dynamic toolbar/dropdown items. Each application calls `Initialize()` once
for a fallback that styles newly opened forms, including plugin dialogs. Apply
custom dialogs explicitly before showing to avoid an initial unstyled frame.
The helper does not replace existing owner-draw handlers; custom renderers should
use `Palette` directly. `PreserveColors(canvas)` excludes asset rendering surfaces.
Tooltips and `ThemedMessageBox` use the same palette. Standard Windows file,
folder and color pickers, native window frames, scrollbars and system accessibility
surfaces remain OS-rendered; the helper does not replace those operating-system controls.

## DockPanel Suite 3.1.1

The supplied VS2015 Dark and Light themes style outer and nested docking surfaces.
The supplied Blue theme was evaluated: its light surfaces do not meet the Dark Blue
requirement. Dark Blue therefore customizes the dark palette, including tabs,
captions, auto-hide surfaces and docking indicators. DockPanel Suite eagerly builds
button/indicator bitmaps; these are regenerated after customizing the palette.
Generated images are owned by the custom theme; shared hotspot resource maps are
not disposed. Every workspace owns a distinct theme and disposes it after its contents.

[ThemeBase.ApplyTo in 3.1.1](https://github.com/dockpanelsuite/dockpanelsuite/blob/Release_3.1.1/WinFormsUI/Docking/ThemeBase.cs)
rejects replacement while panes, float windows or contents exist. All theme changes
therefore require restart, even with no visible document, rather than detaching or
rebuilding editor instances. The same policy applies to inner `EditorDockWorkspace`s.

## Validation

`ThemeTests` in the Studio test project cover palettes and contrast, preference
recovery/sharing, menu exclusivity, unchanged live editor state, dynamic controls,
modal button semantics and docking lifecycle. Existing editor/Studio suites cover
editing and dirty-state behavior.

Run the fresh-process UI probe on Windows for each of `Dark`, `DarkBlue`, `Light`:

```powershell
dotnet run --project Testing/Gondwana.Tooling.ThemeProbe -c Release -- Light <absolute-output-directory>
```

The probe isolates settings and sample files, opens all seven applications and
nested editors, activates tabs, floats/redocks a pane, and disposes each shell.
It saves programmatic control renderings (not desktop screenshots). `--inspect`
keeps the Studio preview open for three minutes for interactive inspection.
Native file dialogs and interactive docking drag targets still warrant a manual
check on the target Windows version and DPI/accessibility configuration.

## Visual previews

These are programmatic renderings of real WinForms controls from the Windows probe,
with empty sample documents. They preserve the dark scene/sprite canvas deliberately.
Desktop screenshot capture was not available because interactive app approval timed out.

| Theme | Studio | Scene editor (property grid and nested panes) |
| --- | --- | --- |
| Dark | [Preview](images/Dark-Studio.png) | [Preview](images/Dark-Scenes.png) |
| Dark Blue | [Preview](images/DarkBlue-Studio.png) | [Preview](images/DarkBlue-Scenes.png) |
| Light | [Preview](images/Light-Studio.png) | [Preview](images/Light-Scenes.png) |
