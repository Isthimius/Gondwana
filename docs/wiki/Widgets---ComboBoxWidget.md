# ComboBoxWidget

`ComboBoxWidget` is a compact single-choice control. It displays the current selection in a button-like header and expands into a `ListBoxWidget` when opened.

Typical uses include difficulty selection, resolution choices, character classes, or other settings where only one value should be selected.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var difficulty = new ComboBoxWidget(
    host,
    view,
    new Rectangle(40, 40, 220, 34),
    new[] { "Easy", "Normal", "Hard" });

difficulty.Placeholder = "Choose difficulty";

difficulty.SelectedIndexChanged += index =>
{
    if (index >= 0)
        gameSettings.Difficulty = difficulty.SelectedItem;
};
```

## Selection

```csharp
difficulty.SelectedIndex = 1;

string? selected = difficulty.SelectedItem;
```

Set `SelectedIndex` to `-1` to clear the selection.

## Managing items

```csharp
difficulty.AddItem("Nightmare");

difficulty.SetItems(new[]
{
    "Story",
    "Normal",
    "Veteran"
});
```

`SetItems()` replaces the list and clears the current selection.

## Opening and closing

Normally the user opens the control by clicking its header. You can also control it directly:

```csharp
difficulty.OpenDropDown();
difficulty.CloseDropDown();
difficulty.ToggleDropDown();
```

The drop-down receives focus while open. Selecting an item with the pointer commits the choice and closes the drop-down; keyboard navigation can move the selection before it is committed.

Moving focus to another widget collapses the drop-down. Its `ListBoxWidget` automatically provides mouse-wheel scrolling, a draggable thumb, and page scrolling when the items exceed the visible rows. Small lists hide the scrollbar by default. Scrolling does not commit a selection or close the drop-down.

Configure this behavior through the existing list:

```csharp
difficulty.DropDown.MouseWheelScrollItems = 3;
difficulty.DropDown.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
```

## Useful members

| Member | Purpose |
| --- | --- |
| `Items` | Read-only view of the available strings. |
| `SelectedIndex` / `SelectedItem` | Current choice. |
| `SelectedIndexChanged` | Raised when selection changes. |
| `Placeholder` | Text displayed while nothing is selected. |
| `IsDropDownOpen` | Reports whether the list is expanded. |
| `Header` | The internal `ButtonWidget`. |
| `DropDown` | The internal `ListBoxWidget`. |
| `SetComboBoxZOrder()` | Sets the visual stacking order. |

`ComboBoxWidget` is available in both view-level and scene-layer forms.
