# ListBoxWidget

`ListBoxWidget` displays a vertically arranged list of strings with one current selection. It supports pointer selection and keyboard navigation.

Use it for inventories, save slots, option lists, server lists, character choices, and similar in-game selection screens.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var saves = new ListBoxWidget(
    host,
    view,
    new Rectangle(40, 40, 280, 160),
    new[]
    {
        "Slot 1 - Forest",
        "Slot 2 - Castle",
        "Slot 3 - Caverns"
    });

saves.SelectedIndexChanged += index =>
{
    preview.Load(index >= 0 ? saves.SelectedItem : null);
};

saves.SelectionCommitted += index =>
{
    LoadSave(index);
};
```

`SelectedIndexChanged` means the current selection moved. `SelectionCommitted` means the user explicitly chose the current row, such as by clicking it or pressing **Enter**.

## Keyboard navigation

The list understands:

- Up / Down
- Home / End
- Page Up / Page Down
- Enter to commit the current selection

## Managing items

```csharp
saves.AddItem("Slot 4 - Tower");

saves.RemoveAt(1);

saves.SetItems(new[]
{
    "New Game",
    "Continue"
});

saves.ClearItems();
```

## Selection and scrolling

```csharp
saves.ItemHeight = 28;
saves.SelectedIndex = 5;

// Changing the selection scrolls that row into view.
int firstVisible = saves.TopIndex;
int rowsVisible = saves.VisibleItemCount;

// Scroll without changing selection.
saves.TopIndex = 2;
saves.MouseWheelScrollItems = 3; // Items per wheel notch; must be positive.
saves.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
```

Wheel-up decreases `TopIndex`; wheel-down increases it. Scrolling clamps at the first and last page without wrapping or changing `SelectedIndex`. Wheel input works over the list, track, or thumb without requiring keyboard focus. A selected row can scroll out of view and retains its highlight when it returns.

`VerticalScrollBarVisibility` defaults to `Auto`, showing a scrollbar only when `Items.Count > VisibleItemCount`. `Always` shows it even when everything fits (with a full-track thumb); `Never` hides it while preserving wheel and keyboard scrolling. `IsVerticalScrollBarVisible` reports whether it is currently shown.

Drag the thumb to scroll continuously, or click above/below it to move one visible page. Thumb size reflects the visible fraction, with an 18-pixel minimum capped by the track height. Scrollbar gestures do not commit selection. Rows reserve space for the scrollbar and text is clipped to each row. Item changes and `ItemHeight` changes refresh the layout automatically. The list has fixed construction bounds; move it with `SetPosition()`.

Keyboard navigation keeps the selected row visible and moves the thumb with it. `SetListBoxZOrder()` also raises the track and thumb. The retained `VerticalScrollBarTrack` and `VerticalScrollBarThumb` rectangles are available for color styling; the list owns their bounds and visibility.

## Useful members

| Member | Purpose |
| --- | --- |
| `Items` | Current strings. |
| `SelectedIndex` / `SelectedItem` | Current selection. |
| `TopIndex` | First visible item. |
| `ItemHeight` | Height of each row. |
| `VisibleItemCount` | Number of complete rows currently visible. |
| `MouseWheelScrollItems` | Items scrolled per wheel notch (default 3). |
| `VerticalScrollBarVisibility` | `Auto`, `Always`, or `Never`. |
| `IsVerticalScrollBarVisible` | Whether the scrollbar is currently shown. |
| `SelectionCommitted` | Explicit user selection event. |
| `SetSelectionColor()` | Changes the selection highlight. |
| `SetListBoxZOrder()` | Sets list visual ordering. |

Both view-level and scene-layer constructors are available.
