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

// The control automatically keeps the selected row visible.
int firstVisible = saves.TopIndex;
int rowsVisible = saves.VisibleItemCount;
```

## Useful members

| Member | Purpose |
| --- | --- |
| `Items` | Current strings. |
| `SelectedIndex` / `SelectedItem` | Current selection. |
| `TopIndex` | First visible item. |
| `ItemHeight` | Height of each row. |
| `VisibleItemCount` | Number of complete rows currently visible. |
| `SelectionCommitted` | Explicit user selection event. |
| `SetSelectionColor()` | Changes the selection highlight. |
| `SetListBoxZOrder()` | Sets list visual ordering. |

Both view-level and scene-layer constructors are available.
