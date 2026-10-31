# WinForms tooling DPI audit

## Scope and initialization

Audited Studio and the six standalone authoring applications on master
`89a5dc277a5171b4400445d693862315a7a98dd1`:

| Application | Startup before this change | Image toolbars |
| --- | --- | --- |
| Studio | Explicit `SystemAware`, visual styles, compatible text rendering off | Global toolbar and embedded definition editors |
| Assets | `ApplicationConfiguration.Initialize()` | Text commands; no custom command glyphs |
| Tilesheets | Generated initialization | Shared definition selector |
| Animations | Generated initialization | Shared definition selector; native text source/sequence/preview bars |
| Audio | Generated initialization | Shared definition selector; native text commands |
| Scenes | Generated initialization | Shared definition selector; native text/hosted-control preview bars |
| Sprites | Generated initialization | Shared definition selector; native text commands |

The projects target .NET 8 Windows with `UseWindowsForms`. None previously set
`ApplicationHighDpiMode` or supplied an application manifest. The standalone
initializers use the WinForms SDK default, SystemAware. All seven now explicitly
set `ApplicationHighDpiMode=SystemAware`; Studio uses the same generated initializer.
There is no custom manifest to conflict with the managed setting.

These are code-first forms and editor controls without designer autoscale dimensions
or explicit AutoScaleMode assignments. The original Studio-style Form reports
AutoScaleMode=Inherit and AutoScaleDimensions=0,0. Native Form/UserControl
font/inheritance behavior is retained. ThemedMessageBox explicitly uses Font autoscaling. Changing
all container autoscaling would affect unrelated docking geometry and is outside
this toolbar fix.

Scene Viewer is a separate rendering host, beyond the six authoring applications.
Its generated initialization and canvas/host lifecycle were inspected as a
PerMonitorV2 compatibility boundary; its awareness and rendering semantics are unchanged.

## Diagnosis and scaling ownership

Explicit ToolStrip item sizes and source bitmap sizes are not a reliable logical-DPI
contract. ToolStripItem is a component, not a child Control that participates in
ordinary container scaling. ToolStrip also manages its own DPI constants and hosted
controls. See the [.NET 8 ToolStrip implementation](https://github.com/dotnet/winforms/blob/v8.0.0/src/System.Windows.Forms/src/System/Windows/Forms/ToolStrip.cs),
especially `RescaleConstantsForDpi` and `ResetScaling`.

The theme probe reproduces the original code-first Studio strip independently of
the new sizing class and prints its post-show bounds, autoscale dimensions, and
image scaling size. This distinguishes automatic strip/font/margin handling from
scaling the explicitly assigned 48px buttons and 24px images. On this Windows host
at 144 DPI, both SystemAware and PerMonitorV2 baseline probes retained 48x48
buttons and ImageScalingSize=24x24 after showing the form. The corrected toolbar
reports 72x72 buttons and 36x36 images. Multiplying current
bounds is unsafe, especially after reparenting or a native scaling pass.

`Tooling/Shared/WinForms/Dpi` supplies one shared-assembly implementation:

* `ToolbarSizing.Pixels` converts immutable 96-DPI logical values with the same
  rounding convention as navigation sizing.
* `DpiToolStrip` assigns absolute button sizes, image sizes, button margins, strip
  padding, and registered selector widths after native scaling, handle creation,
  DPI-after-parent notifications, and layout. Hosted selectors keep their native
  preferred height. This avoids cumulative/double scaling and preferred-width resets.
* `ToolbarGlyphs` draws the existing line geometry and palette directly at the
  target bitmap resolution. Studio's Save All front sheet is inset one logical
  pixel to keep its stroke inside the bitmap.
* The strip owns its generated bitmaps. A DPI/color rebuild replaces and disposes
  each previous image; same-DPI layouts reuse images; disposal clears item references
  and releases the final images. No global image cache or theme subscription is added.

| Effective DPI | Studio button / glyph | Definition button / glyph |
| --- | --- | --- |
| 96 | 48 / 24 | 32 / 32 |
| 120 | 60 / 30 | 40 / 40 |
| 144 | 72 / 36 | 48 / 48 |
| 192 | 96 / 48 | 64 / 64 |

All measurements are device pixels. Logical UX defaults remain Studio 48 and editor
32. Native ToolStrip overflow remains available when the window cannot fit all
commands. Existing text-only bars, menu fonts, commands, keyboard handling,
tooltips, property grids, preview zoom, asset thumbnails, and engine rendering are
unchanged. `TreeNavigationSizing` already uses effective DeviceDpi and handle/DPI
events and served as the reference for ownership and absolute scaling.

Themes are immutable within a process. Selecting a different theme saves a
preference for the next launch. New glyphs use that launch's foreground/accent
palette; the DPI fix does not introduce live theme switching.

## DEBUG diagnostics

The shared appearance code attaches `DpiDiagnostics` to ToolStrips in DEBUG builds.
Debug output records the form/strip identity, effective DeviceDpi, managed
`Application.HighDpiMode`, strip bounds, ImageScalingSize, item bounds, source image
sizes, and margins at attachment, handle creation, DPI notifications, and changed
layouts. Unchanged snapshots are suppressed. The managed awareness value describes
the WinForms setting, not an independent native query of an externally overridden
process manifest/context. Subscriptions and snapshot construction compile out in
Release, with no added normal UI or log file.

## Validation and PerMonitorV2 decision

Automated coverage includes 96/120/144/192 calculations, actual image-button sizes,
selector widths, round trips through DPI values, repeated events, parent scaling,
handle recreation, unchanged-DPI image reuse, disposed old/final bitmaps, and glyph
colors/geometry for every command in all three palettes. Existing command and
composition tests remain in use.

The existing ThemeProbe now checks DPI sizes and prints measurements for real
Studio/editor bars. It also exercises startup, theme menus, nested property grids,
trees and docks, activation, floating/redocking, DrawToBitmap capture, and disposal.
Its reflection call uses the current optional NewDocument parameters.

Run in fresh processes (output folders may be anywhere writable):

```powershell
dotnet build Testing/Gondwana.Tooling.ThemeProbe -c Release
# Repeat for Light, Dark, DarkBlue:
dotnet Testing/Gondwana.Tooling.ThemeProbe/bin/Release/net8.0-windows/Gondwana.Tooling.ThemeProbe.dll Light <output-folder>
# Probe-only evaluation; production settings are not changed:
dotnet Testing/Gondwana.Tooling.ThemeProbe/bin/Release/net8.0-windows/Gondwana.Tooling.ThemeProbe.dll Dark <output-folder> --per-monitor-v2
```

Production retains SystemAware. Passing startup and float/redock at one effective
DPI does not establish PerMonitorV2 compatibility. DockPanelSuite 3.1.1 nested/floating
windows and persisted layouts, property-grid editors, image viewports, animation and
scene preview controls, and Scene Viewer's GPU host need real mixed-DPI transition
validation. These are unverified compatibility gates, not confirmed third-party bugs.
No preview/world-coordinate code was changed to simulate DPI support.

With SystemAware, toolbars render sharply at the system DPI (including 144 DPI),
but Windows may bitmap-scale the whole window after moving to a monitor at another
DPI. DeviceDpi generally remains the system DPI; the new event handling cannot
remove that process-level limitation. Re-launching after a system DPI change may
be necessary.

### Executed checks

Release builds of Studio (which builds all six standalone applications and the
shared assembly) and ThemeProbe completed successfully with Windows targeting
supported by the repository settings. The shared assembly also built in DEBUG.

| Release test project | Passed |
| --- | --- |
| Studio.WinForms.Tests | 339 |
| Tilesheets.WinForms.Tests | 36 |
| Animations.WinForms.Tests | 21 |
| Audio.WinForms.Tests | 12 |
| Scenes.WinForms.Tests | 215 |
| Sprites.WinForms.Tests | 15 |

All 638 tooling tests passed. Assets has no separate WinForms test project; Studio
composition/docking tests and the probe exercise its standalone and embedded paths.
Each project was tested with `dotnet test <project.csproj> -c Release --nologo
/p:EnableWindowsTargeting=true` (Studio's final run used `--no-restore`).

The Windows theme probe passed all seven shells at effective 144 DPI in Light,
Dark, and Dark Blue (21 shell/theme combinations), and all seven in a separate
probe-only PerMonitorV2 Dark process at 144 DPI. Generated Studio and standalone
captures were inspected for glyph alignment and clipping. These are control
renderings, not proof of physical mixed-monitor behavior. The native startup
baseline was reproduced at 144 DPI under both awareness modes.

The full `Testing/Gondwana.Tests/Gondwana.Tests.csproj` engine baseline passed
with **1,175 passed, 1 skipped, 0 failed**, using Release with `--no-build
--no-restore /p:EnableWindowsTargeting=true`. The initial sandbox restore could
not reach NuGet; restoring with network access succeeded. The first full engine
run had one failure in the unchanged
`TextBoxWidget_RepeatedKeyActionsHonorInitialDelayAndRepeatInterval` test
(expected `a`, observed `aa` at its 50ms immediate-repeat assertion). That test
passed in isolation, and the subsequent complete baseline passed. No widget or
engine behavior was changed to address the timing-sensitive failure.

Existing nullable/XML documentation warnings and intermittent NuGet vulnerability
metadata warnings were present. The full solution/workload build was not run;
validation focused on the relevant Windows projects and the engine baseline.

### Manual Windows matrix still required before enabling PerMonitorV2

1. Start each of the seven apps on monitors at 100%, 125%, 150%, and 200%.
   Check startup bounds against the table, hover tooltips, enabled/disabled glyphs,
   dropdown arrows, toolbar overflow, keyboard shortcuts, and command behavior.
2. In a temporary PerMonitorV2 build (all seven project settings), move the main
   window and floating tool/document windows between unequal-DPI monitors in both
   directions, repeatedly. Capture DEBUG output before/after each move. Check glyph
   sharpness, button bounds, selector height/width, property-grid popups, docking
   targets, hit testing, and absence of cumulative growth.
3. Open all definition formats and a real asset archive. Exercise animation/scene
   previews, viewport zoom/pan/selection, and Scene Viewer. Verify cursor-to-content
   alignment and rendering after each transition without changing game coordinates.
4. Save docking layouts on each monitor, restart/restore on another monitor,
   float/redock nested editor panes, hide/show panes, and reset layout. Check for
   inaccessible panes, clipping, and exceptions.
5. Repeat with Dark, Dark Blue, and Light in separate launches. Repeatedly move,
   create/close documents, and dispose windows while watching GDI object counts
   for sustained growth. Unit disposal checks do not replace this long-run test.

Real monitor moves, display scaling changes, interactive popups, visual sharpness,
and live rendering/hit testing cannot be established by the arithmetic tests or
DrawToBitmap smoke probe alone.
