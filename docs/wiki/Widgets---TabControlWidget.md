# TabControlWidget

`TabControlWidget` groups related interface content into selectable pages. Each tab owns a `TabPageWidget`, which is a normal widget container: add labels, buttons, text boxes, lists, or other widgets to the page and Gondwana handles showing, hiding, input routing, and composite movement with the selected tab.

Use it for settings screens, character sheets, inventory categories, in-game manuals, developer tools, and other panels with related sections.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view` or a `SceneLayer` named `sceneLayer`.

## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var tabs = new TabControlWidget(
    host,
    view,
    new Rectangle(40, 40, 480, 300));

TabPageWidget general = tabs.AddTab("General", mnemonic: 'G');
TabPageWidget audio = tabs.AddTab("Audio", mnemonic: 'A');

general.AddWidget(
    new CheckBoxWidget(
        host,
        view,
        new Rectangle(0, 0, 240, 30),
        "Show hints",
        isChecked: true),
    new Point(16, 18));

audio.AddWidget(
    new CheckBoxWidget(
        host,
        view,
        new Rectangle(0, 0, 240, 30),
        "Music enabled",
        isChecked: true),
    new Point(16, 18));

tabs.Show();
```

Child offsets passed to `TabPageWidget.AddWidget()` are relative to the upper-left corner of the page content area, below the tab headers. Only the selected page is visible and able to receive input.

Select pages in code with either the index or page object:

```csharp
tabs.SelectTab(1);
tabs.SelectTab(audio);
```

## Header rows and sizing

By default, `TabControlWidget` has one header row. Its visible headers divide the available control width evenly, and each header label uses the same bounds as its header.

Reserve a fixed number of rows when the tab arrangement is known:

```csharp
tabs.TabRowCount = 2;
```

With two rows, tabs fill cells from left to right and then top to bottom. Empty cells at the end of the last row remain unused.

For a control that grows additional rows as pages are added, use `AutoExpandRows`. `PreferredTabWidth` determines how many columns are retained before another row is created:

```csharp
tabs.AutoExpandRows = true;
tabs.PreferredTabWidth = 120;
```

The tab height is configurable in native pixels:

```csharp
tabs.TabHeight = 36;
```

Read `DisplayedTabRowCount` and `DisplayedTabColumnCount` when surrounding layout needs to account for the current tab grid.

## Keyboard mnemonics

Give each tab a distinct `mnemonic` to let users select it with `Alt+<key>`:

```csharp
tabs.AddTab("Graphics", mnemonic: 'G');
tabs.AddTab("Controls", mnemonic: 'C');
```

The widget input router offers an unhandled keyboard event to tab controls after the focused widget has had a chance to consume it. That keeps a focused text editor or custom control in charge when it explicitly handles a key.

## Reordering

Tab drag reordering is opt-in:

```csharp
tabs.IsTabReorderingEnabled = true;
tabs.TabReorderDragThresholdPx = 6f;

tabs.TabReordered += (page, fromIndex, toIndex) =>
{
    SaveTabOrder(page, fromIndex, toIndex);
};
```

Users can then drag a header over another header to move it. The originating page remains selected if it was already selected. To rearrange pages in code, use `MoveTab`:

```csharp
tabs.MoveTab(fromIndex: 3, toIndex: 0);
```

## Styling and layering

```csharp
tabs
    .SetTabColors(
        normal: Color.FromArgb(255, 60, 60, 68),
        hover: Color.FromArgb(255, 78, 78, 88),
        pressed: Color.FromArgb(255, 42, 42, 48),
        selected: Color.FromArgb(255, 48, 89, 142))
    .SetTabTextColor(Color.White)
    .SetTabControlZOrder(500);
```

`TabPageWidget` exposes its `Background` and has the same familiar background and border helpers as a panel:

```csharp
general
    .SetBackgroundColor(Color.FromArgb(235, 25, 28, 36))
    .SetBorderColor(Color.FromArgb(255, 110, 120, 140))
    .SetCornerRadius(6f);
```

## View and SceneLayer placement

`TabControlWidget` supports both normal view-relative UI and world-space UI attached to a scene layer:

```csharp
var terminalTabs = new TabControlWidget(
    host,
    sceneLayer,
    new Rectangle(160, 96, 420, 260));
```

All pages and their child widgets must use the same target as the tab control. For conventional menus, settings, and HUDs, prefer the view-level constructor.

## Useful members

| Member | Purpose |
| --- | --- |
| `AddTab()` / `RemoveTab()` | Adds or removes a page and its header. |
| `Tabs` | Current pages in visible tab order. |
| `SelectedIndex` / `SelectedTab` | Current selection. |
| `SelectTab()` | Selects a page in code. |
| `TabRowCount` | Fixed number of header rows; default is one. |
| `AutoExpandRows` / `PreferredTabWidth` | Adds rows automatically as tabs are added. |
| `IsTabReorderingEnabled` | Enables primary-pointer header dragging. |
| `MoveTab()` | Reorders pages programmatically. |
| `SelectedTabChanged` / `TabReordered` | Selection and ordering notifications. |
| `SetTabColors()` / `SetTabTextColor()` | Header styling. |
| `SetTabControlZOrder()` | Places page visuals and headers together in draw order. |

The `Demos/WidgetsTest` project includes an executable tab-control example with page widgets, Alt mnemonics, automatic rows, and drag reordering.
