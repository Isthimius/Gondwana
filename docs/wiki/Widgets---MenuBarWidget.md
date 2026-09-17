# Menu bars and nested menus

`Gondwana.Widgets.Menus` provides view-relative menus for games and application-like tools. The family uses ordinary retained DirectDrawing visuals and the shared widget input router on both CPU and GPU rendering paths.

`MenuBarWidget` coordinates top-level `MenuHeaderWidget`/`MenuDropDownWidget` pairs, exposed as `MenuBarMenu`. Each actionable row is a `MenuItemWidget`: a command, check item, radio item, or submenu owner. Separators remain non-actionable. `AddMenu`, `AddItem`, `AddCheckItem`, `AddRadioItem`, `AddSubMenu`, and `AddSeparator` return their containing bar/dropdown for fluent construction.

## Build a menu

```csharp
using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Widgets.Menus;

var menuBar = new MenuBarWidget(host, view,
    new Rectangle(0, 0, view.Viewport.TargetRectPx.Width, 32));

menuBar
    .AddMenu("File", file => file
        .AddItem("New", NewProject, key: "file.new",
            shortcut: KeyGesture.Ctrl('N'), mnemonic: 'N')
        .AddItem("Open...", OpenProject, key: "file.open",
            shortcut: KeyGesture.Ctrl('O'), mnemonic: 'O')
        .AddSubMenu("Recent Files", recent => recent
            .AddItem("Game.gws", () => OpenRecent("Game.gws"))
            .AddItem("Demo.gws", () => OpenRecent("Demo.gws")), mnemonic: 'R')
        .AddSubMenu("Export", export => export
            .AddSubMenu("Image", image => image
                .AddItem("PNG", ExportPng, key: "file.export.png",
                    shortcut: KeyGesture.CtrlShift('P'), mnemonic: 'P')
                .AddItem("JPEG", ExportJpeg, mnemonic: 'J'), mnemonic: 'I'), mnemonic: 'E')
        .AddSeparator()
        .AddItem("Save", SaveProject, enabled: false, key: "file.save",
            shortcut: KeyGesture.Ctrl('S'), mnemonic: 'S')
        .AddSeparator()
        .AddItem("Exit", ExitApplication, mnemonic: 'X'), mnemonic: 'F')
    .AddMenu("View", menu => menu
        .AddCheckItem("Show Grid", SetGridVisible, isChecked: true,
            key: "view.grid", mnemonic: 'G')
        .AddSeparator()
        .AddRadioItem("Pixels", UsePixels, "units", isChecked: true,
            key: "view.pixels", mnemonic: 'P')
        .AddRadioItem("Tiles", UseTiles, "units", key: "view.tiles", mnemonic: 'T'), mnemonic: 'V')
    .AddMenu("Help", help => help
        .AddItem("About", ShowAbout, mnemonic: 'A'), mnemonic: 'H');

menuBar.Show();

// Named methods contain the application behavior; no switch over item IDs.
void NewProject() { /* create a project */ }
void OpenProject() { /* open a project */ }
void OpenRecent(string path) { /* open this path */ }
void SaveProject() { /* save the current project */ }
void ExportPng() { /* export PNG */ }
void ExportJpeg() { /* export JPEG */ }
void ExitApplication() { /* request application shutdown */ }
void SetGridVisible(bool isChecked) { /* grid.Visible = isChecked; */ }
void UsePixels() { /* use pixel coordinates */ }
void UseTiles() { /* use tile coordinates */ }
void ShowAbout() { /* show about information */ }
```

The normal host creates a `WidgetInputRouter`. Keyboard polling remains explicitly configured by the application: call `Engine.Input.KeyboardEventPoller.StartMonitoringKey(...)` for navigation keys, shortcut keys, and mnemonic keys, as with other widgets. The GPU `Demos/WidgetsTest/WidgetsTestHost.cs` sample includes this setup, a focused text editor, status output, and all the menu variations above.

## Stable keys and references

Keys identify items independently of their displayed text. They are optional, nonblank when supplied, case-sensitive, and unique across the whole bar, including every submenu. A duplicate throws `ArgumentException`; failed menu configuration disposes its partial hierarchy and rolls back registrations. `Key` is read-only. Unkeyed commands may have identical labels.

```csharp
MenuItemWidget saveItem = menuBar.GetItem("file.save");
saveItem.SetEnabled(documentIsDirty);
menuBar["view.grid"].SetChecked(showGrid);
if (menuBar.TryGetItem("file.export.png", out MenuItemWidget? png))
    png.SetEnabled(canExport);
```

`GetItem` and the indexer throw `KeyNotFoundException` for a missing key. `TryGetItem` returns `false` and `null`. `Menus` and `Items` are read-only views. A direct reference from `Items` is also suitable for unkeyed items. Disposing an item removes its key and shortcut registration; disposing the bar recursively disposes its dropdowns, items, and visuals.

## Invocation and state

Each item retains its own callback. `PerformClick()` is the common path used by pointer activation, Enter/Space, shortcuts, and item mnemonics. It can execute a command while its dropdown is closed, provided the bar is visible/input-enabled and the item and every submenu-owner ancestor are enabled. Disabled entries are skipped during navigation and cannot invoke or alter checked state through invocation.

For commands the order is:

1. Verify availability.
2. Toggle a check item, or select a radio item and clear its group peers.
3. Refresh visual state.
4. Close the entire open hierarchy immediately, releasing dismissal input and restoring prior focus when appropriate.
5. Raise the bar's `ItemInvoked`, then the item's `Invoked` notification.
6. Execute the item callback. A check callback receives the resulting Boolean state.

Callbacks can open dialogs or dispose UI because the hierarchy is already closed. Notification events are useful for observation, not required for command dispatch. As with normal .NET events, exceptions from observers propagate.

`SetChecked` changes state and visuals without invoking callbacks or invocation events. It throws for a non-checkable item. Radio groups use exact group names and are local to their containing dropdown: checking one clears its peers even programmatically. An already selected radio command stays checked when invoked. The last initially checked radio item wins; programmatically clearing a selection is allowed.

`SetEnabled` updates visuals/input immediately and closes that item's open submenu. Enabling an item while closed does not activate an invisible pointer target.

## Shortcuts and keyboard routing

`KeyGesture` is a small value type with an integer adapter key code and the existing `KeyboardModifierState` flags. Matching is exact: Ctrl+X does not match Ctrl+Shift+X. Use uppercase letter key codes, e.g. `KeyGesture.Ctrl('O')`. Arbitrary adapter codes are accepted; built-in display names follow the virtual-key conventions used by the current widgets. There is no new modifier enum or platform dependency.

```csharp
KeyGesture.Ctrl('S');
KeyGesture.CtrlShift('S');
new KeyGesture(115, KeyboardModifierState.Alt); // Alt+F4 in the standard widget key convention
```

Gesture-generated display text takes precedence over legacy `shortcutText`. Legacy strings remain display-only and are never parsed as executable shortcuts. Duplicate gestures within a bar are rejected, including conflicts in different submenu branches. Gestures are immutable once the item is created.

The router gives the focused widget (and its normal container bubbling path) the first opportunity to handle a key. If handled, routing stops. Otherwise registered, visible, input-enabled menu bars may process it, most recently activated/registered first. The first bar to handle it stops fallback, so one keypress invokes at most one menu command. Hidden, disposed, or detached-view bars are ineligible. Closed bars do not acquire focus for shortcuts. Opening a menu temporarily focuses a header; closing restores the prior eligible widget. This lets a text editor consume editing commands before menu accelerators see them.

Only initial `Pressed` events activate menus; repeated/released events do not repeatedly execute commands. Register gestures for keys the host actually emits and monitors.

## Mnemonics and navigation

Specify access characters explicitly with `mnemonic: 'F'`. Display labels are not parsed or modified; there is no ampersand notation or automatic underline. Matching is case-insensitive. Duplicate header mnemonics within a bar and duplicate item mnemonics within one dropdown are rejected.

Alt+F opens the corresponding top-level menu and selects its first enabled item. While a dropdown is open, an unmodified item mnemonic invokes a command or opens a child submenu. Focused-widget handling takes precedence over mnemonic fallback too.

| Key | Behavior |
| --- | --- |
| Up / Down | Select previous/next enabled item in the active dropdown, wrapping and skipping separators |
| Right | Open the selected submenu and select its first enabled item; at the root without a submenu, switch to the next header |
| Left | Close one child level and return to its owner; at the root, switch to the previous header |
| Enter / Space | Invoke the selected command or open its submenu |
| Escape | Close one child level; at the root, close the bar's dropdown |

Pointer hover opens child submenus while the hierarchy is active. Selecting another row closes the prior child branch. Hovering another top-level header switches menus. Clicking outside closes the whole hierarchy.

## Recursive ownership, placement, and animation

Every dropdown owns its row widgets and child dropdowns through `ContainerWidget`. The submenu owner exposes `SubMenu`; there is no depth limit or separate implementation for a second nesting level. Keyboard navigation follows the deepest open child; closing/disposal unwinds recursively. Each child has a higher drawing Z-order and is activated later in pointer routing than its parent.

Child dropdowns open to the right when possible, flip to the left at the viewport edge, and clamp vertically. Placement uses the current View target rectangle at every level. Menus too large for a viewport are anchored at its edge; scrolling menus are not provided.

`MenuDropDownAnimation.None`, `Fade`, and `FadeAndReveal` apply to each popup. `DropDownAnimationDurationSec` defaults to 0.13 seconds. Closing a branch immediately hides/cancels child popup animations so no descendant survives its parent. Command invocation closes immediately before callbacks. Public `MenuOpened` and `MenuClosed` observe top-level transitions; `MenuClosed` occurs when closing begins, not at the end of a fade.

## Icons, layout, and theme

Pass an optional `SKImage` through `icon:` on any item-building method. Menus use `DirectImage`, sharing normal visibility, opacity, reveal, and disposal behavior. The caller owns the image and must keep it alive until the menu is disposed. Disabled icons receive the theme's disabled tint.

Rows reserve separate columns for check/radio markers, icons, labels, shortcut text, and arrows. A column is reserved across the dropdown only when needed. Thus an item can have both a checkmark and an icon. Skia font measurements determine label and shortcut width; columns cannot overlap. `SetWidth` sets a minimum, and content may increase it.

`MenuBarTheme` retains existing dimensions/colors and adds `MarkerColumnWidth`, `IconSize`, `IconGap`, `SubMenuArrowWidth`, `SubMenuGap`, `IndicatorSize`, `IndicatorStrokeWidth`, and `IndicatorColor`. Existing code needs no new theme configuration. Indicators use geometry so they do not depend on font glyph coverage. Checked check items display a checkmark; selected radio items display a filled dot. Unchecked markers keep the same reserved column.

See [[Widgets Overview|Widgets-Overview]] for shared lifecycle/input behavior and [[DirectDrawing]] for the rendering model.
