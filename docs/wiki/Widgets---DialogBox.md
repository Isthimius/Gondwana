# DialogBox

`DialogBox` is the base class for Gondwana's draggable in-game dialogs. It supplies the common window behavior: panel, title bar, optional close button, dragging, **Enter**/**Escape** handling, result values, and close lifecycle.

`DialogBox` is abstract, so normal game code does not instantiate it directly. Use `AboutBox` for the built-in About dialog, or derive a small game-specific dialog when you need custom content.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## What it provides

A derived dialog automatically receives:

- a panel and title bar;
- an optional close button;
- dragging by the title bar;
- `AcceptKey` (Enter by default);
- `CancelKey` (Escape by default);
- `DialogResult`;
- a `Closed` event;
- optional automatic disposal after closing.

## Minimal custom dialog

```csharp
using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;

public sealed class ConfirmDialog : DialogBox
{
    public ButtonWidget YesButton { get; }

    public ConfirmDialog(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
        : base(host, view, bounds, "Confirm")
    {
        YesButton = new ButtonWidget(
            host,
            view,
            new Rectangle(bounds.Left + 24, bounds.Bottom - 54, 100, 34),
            "Yes");

        Add(YesButton);
        YesButton.Clicked += () => Close(DialogResult.OK);
    }
}
```

## Showing and handling the result

```csharp
var confirm = new ConfirmDialog(
    host,
    view,
    new Rectangle(140, 100, 360, 220));

confirm.Closed += result =>
{
    if (result == DialogResult.OK)
        DeleteSave();
};

confirm.Show();
confirm.Activate();
```

## Closing

```csharp
confirm.Close(DialogResult.Cancel);
```

By default, closing a dialog also disposes it. To keep the instance for reuse:

```csharp
confirm.DisposeOnClose = false;
```

## Keyboard behavior

```csharp
confirm.AcceptKey = 13; // Enter
confirm.CancelKey = 27; // Escape
```

Derived dialogs can override `OnAcceptRequested()` when Enter should perform a custom action.

## Useful members

| Member | Purpose |
| --- | --- |
| `Closed` | Raised with the final `DialogResult`. |
| `Close()` | Closes the dialog with a chosen result. |
| `Result` | Result used when the dialog closed. |
| `IsClosed` | Whether it has already closed. |
| `DisposeOnClose` | Controls automatic disposal. |
| `Panel` / `TitleBar` / `TitleText` | Built-in dialog visuals. |
| `CloseButton` | Optional built-in close button. |
