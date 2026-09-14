Gondwana widgets are interactive, movable composites built from the engine's direct-drawing primitives. A custom widget normally derives from `WidgetBase`, adds one or more direct-drawing children, and overrides the appropriate lifecycle or input hooks.

This guide covers:

- Choosing the correct widget base class
- Creating a complete custom leaf widget
- Supporting both view-space and scene-layer-space widgets
- Handling pointer, keyboard, and focus input
- Creating container and draggable widgets
- Managing visibility, activation, ownership, and disposal
- Avoiding common composition and input-routing mistakes

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Choosing a Base Class](#choosing-a-base-class)
- [How Widget Composition Works](#how-widget-composition-works)
- [Complete Example: `CounterWidget`](#complete-example-counterwidget)
- [Creating and Showing the Widget](#creating-and-showing-the-widget)
  - [View-level widget](#view-level-widget)
  - [Scene-layer widget](#scene-layer-widget)
- [Widget Lifecycle](#widget-lifecycle)
  - [`Show()`](#show)
  - [`Hide()`](#hide)
  - [`Activate()`](#activate)
  - [`Cancel()`](#cancel)
- [Input Configuration](#input-configuration)
  - [`IsInputEnabled`](#isinputenabled)
  - [`IsPointerInputEnabled`](#ispointerinputenabled)
  - [`IsKeyboardInputEnabled`](#iskeyboardinputenabled)
  - [`CanReceiveFocus`](#canreceivefocus)
- [Overriding Input Hooks](#overriding-input-hooks)
- [Hit Testing](#hit-testing)
- [Adding Children and Local Offsets](#adding-children-and-local-offsets)
- [Creating a Container Widget](#creating-a-container-widget)
- [Creating a Draggable Widget](#creating-a-draggable-widget)
  - [Restricting the drag area](#restricting-the-drag-area)
  - [Constraining movement](#constraining-movement)
- [Events or Overrides?](#events-or-overrides)
- [Visibility, Focus, and Input State](#visibility-focus-and-input-state)
- [Drawing Order and Input Order](#drawing-order-and-input-order)
  - [Drawing order](#drawing-order)
  - [Input order](#input-order)
- [Ownership and Disposal](#ownership-and-disposal)
- [Common Mistakes](#common-mistakes)
- [Custom Widget Checklist](#custom-widget-checklist)
- [Recommended Design Pattern](#recommended-design-pattern)

---

## Prerequisites

Reference the packages or projects that contain:

```text
Gondwana
Gondwana.Widgets
```

A typical custom widget uses these namespaces:

```csharp
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets;
using SkiaSharp;
using System.Drawing;
using System.Numerics;
```

Your application must also initialize widget input for its render-surface host. The official Gondwana hosting adapters normally handle this integration. Calling `Show()` registers the widget with the input router attached to that host.

---

## Choosing a Base Class

Choose the narrowest base class that matches the widget's behavior.

| Base class | Use it for |
|---|---|
| `WidgetBase` | A single interactive widget composed from direct drawings |
| `DraggableWidgetBase` | A leaf widget that can be dragged |
| `ContainerWidget` | A widget that owns and coordinates other widgets |
| `DraggableContainerWidget` | A container whose entire contents move together |
| `DialogBox` | A dialog-style container with title-bar and close behavior |

A widget does not need to render itself directly. `WidgetBase` inherits from `DirectComposite`, so the widget owns and coordinates its direct-drawing children.

Typical child types include:

```text
DirectRectangle
TextBlock
DirectImage
DirectComposite
Other IDirectCompositeChild implementations
```

---

## How Widget Composition Works

Every widget has an anchor position. Children retain a local offset from that anchor.

```text
Widget anchor
├── background at offset (0, 0)
├── icon at offset (8, 8)
└── label at offset (40, 8)
```

Moving the widget moves every child:

```csharp
widget.SetPosition(new Vector2(200, 120));
```

The following rules apply:

1. Every child must implement `IDirectCompositeChild`.
2. Every child must use the same `RenderSurfaceHostBase` as its parent.
3. Every child must use the same `DirectDrawingMode`.
4. View-mode children must use the same `View`.
5. Scene-layer-mode children must use the same `SceneLayer`.
6. A child may belong to only one composite at a time.
7. Recursive parent-child cycles are rejected.
8. Disposing the widget disposes the children it owns.

The first child establishes the widget's `View` or `SceneLayer`.

---

## Complete Example: `CounterWidget`

The following widget displays a number and increments it when clicked or when the focused widget receives Enter or Space.

It supports both:

- `DirectDrawingMode.View`, for fixed screen-space UI
- `DirectDrawingMode.SceneLayer`, for UI embedded in the game world

```csharp
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets;
using SkiaSharp;
using System.Drawing;

namespace MyGame.Widgets;

/// <summary>
/// Displays a numeric value that can be incremented through pointer or
/// keyboard input.
/// </summary>
public sealed class CounterWidget : WidgetBase
{
    private static readonly Color _backgroundColor =
        Color.FromArgb(255, 48, 48, 56);

    private static readonly Color _normalBorderColor =
        Color.FromArgb(255, 150, 150, 160);

    private static readonly Color _focusedBorderColor =
        Color.Gold;

    /// <summary>
    /// Occurs after the counter value changes.
    /// </summary>
    public event Action<int>? ValueChanged;

    /// <summary>
    /// Initializes a view-level counter widget.
    /// </summary>
    /// <param name="renderSurfaceHost">
    /// The render surface host that owns the widget.
    /// </param>
    /// <param name="view">
    /// The view in which the widget is displayed.
    /// </param>
    /// <param name="bounds">
    /// The widget bounds in screen pixels.
    /// </param>
    /// <param name="initialValue">
    /// The initial counter value.
    /// </param>
    /// <param name="nickname">
    /// An optional diagnostic nickname.
    /// </param>
    public CounterWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle bounds,
        int initialValue = 0,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            bounds.Location,
            nickname)
    {
        Background = CreateBackground(
            renderSurfaceHost,
            view,
            bounds);

        Label = CreateLabel(
            renderSurfaceHost,
            view,
            bounds);

        CompleteInitialization(initialValue);
    }

    /// <summary>
    /// Initializes a scene-layer counter widget.
    /// </summary>
    /// <param name="renderSurfaceHost">
    /// The render surface host that owns the widget.
    /// </param>
    /// <param name="sceneLayer">
    /// The scene layer in which the widget is displayed.
    /// </param>
    /// <param name="bounds">
    /// The widget bounds in world pixels.
    /// </param>
    /// <param name="initialValue">
    /// The initial counter value.
    /// </param>
    /// <param name="nickname">
    /// An optional diagnostic nickname.
    /// </param>
    public CounterWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer,
        Rectangle bounds,
        int initialValue = 0,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            bounds.Location,
            nickname)
    {
        Background = CreateBackground(
            renderSurfaceHost,
            sceneLayer,
            bounds);

        Label = CreateLabel(
            renderSurfaceHost,
            sceneLayer,
            bounds);

        CompleteInitialization(initialValue);
    }

    /// <summary>
    /// Gets the rectangle used for the widget background and border.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the text block used to display the current value.
    /// </summary>
    public TextBlock Label { get; }

    /// <summary>
    /// Gets the current counter value.
    /// </summary>
    public int Value { get; private set; }

    /// <summary>
    /// Sets the current counter value.
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <returns>The current widget.</returns>
    public CounterWidget SetValue(int value)
    {
        Value = value;
        Label.SetText(Value.ToString());

        ValueChanged?.Invoke(Value);

        return this;
    }

    /// <summary>
    /// Increments the current value by one.
    /// </summary>
    /// <returns>The current widget.</returns>
    public CounterWidget Increment()
    {
        return SetValue(Value + 1);
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(
        WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        Increment();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(
        WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed)
            return;

        // Standard Enter and Space virtual-key values.
        if (args.Key is not 13 and not 32)
            return;

        args.Handled = true;
        Increment();
    }

    /// <inheritdoc/>
    protected override void OnFocusGained()
    {
        base.OnFocusGained();

        UpdateBorderColor(_focusedBorderColor);
    }

    /// <inheritdoc/>
    protected override void OnFocusLost()
    {
        base.OnFocusLost();

        UpdateBorderColor(_normalBorderColor);
    }

    private void CompleteInitialization(int initialValue)
    {
        Add(Background);
        Add(Label);

        Background.ZOrder = 0;
        Label.ZOrder = 1;

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;

        Value = initialValue;
        Label.SetText(Value.ToString());
    }

    private void UpdateBorderColor(Color color)
    {
        Background.SetBorderColor(color);

        // Reapply the current position so the rectangle's old and new regions
        // are marked for refresh without changing its location.
        Background.SetPosition(
            Background.GetPosition());
    }

    private static DirectRectangle CreateBackground(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
    {
        return new DirectRectangle(
                _backgroundColor,
                host,
                view,
                bounds)
            .SetFilled(true)
            .SetBorderColor(_normalBorderColor)
            .SetStrokeWidth(2f)
            .SetCornerRadius(6f);
    }

    private static DirectRectangle CreateBackground(
        RenderSurfaceHostBase host,
        SceneLayer sceneLayer,
        Rectangle bounds)
    {
        return new DirectRectangle(
                _backgroundColor,
                host,
                sceneLayer,
                bounds)
            .SetFilled(true)
            .SetBorderColor(_normalBorderColor)
            .SetStrokeWidth(2f)
            .SetCornerRadius(6f);
    }

    private static TextBlock CreateLabel(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
    {
        return new TextBlock(
                host,
                view,
                bounds)
            .SetText(string.Empty)
            .SetFont(
                SKTypeface.Default,
                18f,
                minSize: 10f)
            .SetColors(
                SKColors.White,
                SKColors.Transparent)
            .SetAlignment(
                SKTextAlign.Center,
                TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);
    }

    private static TextBlock CreateLabel(
        RenderSurfaceHostBase host,
        SceneLayer sceneLayer,
        Rectangle bounds)
    {
        return new TextBlock(
                host,
                sceneLayer,
                view: null,
                worldBounds: bounds)
            .SetText(string.Empty)
            .SetFont(
                SKTypeface.Default,
                18f,
                minSize: 10f)
            .SetColors(
                SKColors.White,
                SKColors.Transparent)
            .SetAlignment(
                SKTextAlign.Center,
                TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);
    }
}
```

---

## Creating and Showing the Widget

### View-level widget

```csharp
var counter = new CounterWidget(
    renderSurfaceHost,
    view,
    new Rectangle(
        x: 24,
        y: 24,
        width: 120,
        height: 48),
    initialValue: 5,
    nickname: "hud.counter");

counter.ValueChanged += value =>
{
    Console.WriteLine(
        $"Counter changed to {value}.");
};

counter.Show();
counter.Activate();
```

A view-level widget stays fixed in screen space and is associated with one `View`.

### Scene-layer widget

```csharp
var counter = new CounterWidget(
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(
        x: 640,
        y: 320,
        width: 120,
        height: 48),
    initialValue: 5,
    nickname: "world.counter");

counter.Show();
```

A scene-layer widget uses world-pixel coordinates. Each view that displays the scene layer can project and hit-test the widget independently.

---

## Widget Lifecycle

`WidgetBase` provides four primary lifecycle methods:

```csharp
widget.Show();
widget.Hide();
widget.Activate();
widget.Cancel();
```

### `Show()`

`Show()`:

1. Registers the widget with the input router attached to its host.
2. Makes its descendants visible.
3. Executes the show-processing hook.
4. Calls `OnShown()`.
5. Raises `Shown`.

### `Hide()`

`Hide()`:

1. Releases active pointer and focus state.
2. Makes the widget invisible.
3. Executes the hide-processing hook.
4. Calls `OnHidden()`.
5. Raises `Hidden`.

### `Activate()`

`Activate()` moves the widget to the front of the input-routing order, then calls the activation hooks and raises `Activated`.

Input order and drawing order are separate:

- `Activate()` affects which overlapping widget receives input first.
- Child `ZOrder` values affect drawing order.

### `Cancel()`

`Cancel()` invokes cancellation behavior without automatically assuming what cancellation means for a particular widget. A custom widget may override `OnCancelled()` to hide itself, reset state, or raise application-specific behavior.

```csharp
/// <inheritdoc/>
protected override void OnCancelled()
{
    base.OnCancelled();
    Hide();
}
```

---

## Input Configuration

Every widget has several independent input settings.

```csharp
IsInputEnabled = true;
IsPointerInputEnabled = true;
IsKeyboardInputEnabled = true;
CanReceiveFocus = true;
```

### `IsInputEnabled`

The master switch for all routed widget input.

Setting it to `false` releases current pointer and focus state.

### `IsPointerInputEnabled`

Controls pointer hit testing and pointer callbacks.

Set it to `false` for a visual-only widget that should not block widgets behind it.

```csharp
IsPointerInputEnabled = false;
```

### `IsKeyboardInputEnabled`

Allows keyboard events to be routed to the focused widget.

### `CanReceiveFocus`

Allows the input router to assign keyboard focus to the widget.

A keyboard-interactive widget normally enables both:

```csharp
CanReceiveFocus = true;
IsKeyboardInputEnabled = true;
```

---

## Overriding Input Hooks

Use the `On...` hooks for custom widget behavior:

```csharp
protected override void OnPointerEnter(
    WidgetPointerEventArgs args)
{
    base.OnPointerEnter(args);
}

protected override void OnPointerLeave(
    WidgetPointerEventArgs args)
{
    base.OnPointerLeave(args);
}

protected override void OnPointerDown(
    WidgetPointerEventArgs args)
{
    base.OnPointerDown(args);
}

protected override void OnPointerMove(
    WidgetPointerEventArgs args)
{
    base.OnPointerMove(args);
}

protected override void OnPointerUp(
    WidgetPointerEventArgs args)
{
    base.OnPointerUp(args);
}

protected override void OnPointerClick(
    WidgetPointerEventArgs args)
{
    base.OnPointerClick(args);
}

protected override void OnKeyboardInput(
    WidgetKeyboardEventArgs args)
{
    base.OnKeyboardInput(args);
}

protected override void OnFocusGained()
{
    base.OnFocusGained();
}

protected override void OnFocusLost()
{
    base.OnFocusLost();
}
```

The dispatch sequence is:

```text
Process... hook
On... hook
Public event
```

The `Process...` hooks exist for reusable base-class behavior. Application and custom-widget behavior should normally be implemented in the `On...` hooks.

Set `args.Handled = true` after consuming an interaction:

```csharp
protected override void OnPointerClick(
    WidgetPointerEventArgs args)
{
    base.OnPointerClick(args);

    if (!args.IsPrimaryButton)
        return;

    args.Handled = true;

    // Perform the widget action.
}
```

`WidgetPointerEventArgs` provides:

```text
View
ScreenPositionPx
Button
ClickCount
DeltaPx
PointerId
IsPrimaryButton
Handled
Tick
```

`WidgetKeyboardEventArgs` provides:

```text
Key
KeyAction
Modifiers
Handled
Tick
```

---

## Hit Testing

The default hit test uses the union of the widget's visible descendants.

For most rectangular widgets, no override is required.

Override `HitTest` for:

- Nonrectangular widgets
- Transparent regions that should allow pointer passthrough
- Widgets with a deliberately smaller interaction area
- Interaction rules not represented by the visual bounds

```csharp
/// <inheritdoc/>
public override bool HitTest(
    View view,
    Point screenPositionPx)
{
    if (!base.HitTest(
            view,
            screenPositionPx))
    {
        return false;
    }

    // Add custom shape or state checks here.
    return true;
}
```

Retain the base checks unless the widget intentionally replaces Gondwana's input-enabled, visibility, mode, and view validation.

---

## Adding Children and Local Offsets

The simplest pattern is to construct child drawings at their final coordinates and preserve their offset when adding them:

```csharp
Add(Background);
Add(Label);
```

To assign an explicit local offset from the widget anchor:

```csharp
Add(
    child,
    keepCurrentOffset: false,
    explicitLocalOffsetPx:
        new Vector2(
            12,
            8));
```

To change an existing child's offset:

```csharp
SetLocalOffset(
    child,
    new Vector2(
        24,
        16));
```

To detach a child without disposing it:

```csharp
Remove(child);
```

After removal, the child may be attached to another composite.

---

## Creating a Container Widget

Use `ContainerWidget` when a widget owns other independently interactive widgets.

A container automatically:

- Shows and hides child widgets with the parent
- Activates children after the parent
- Cancels child widgets with the parent
- Disposes owned child widgets
- Bubbles unhandled keyboard input from a child to the container

A minimal container might look like this:

```csharp
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using System.Drawing;
using System.Numerics;

namespace MyGame.Widgets;

/// <summary>
/// Provides a small toolbar containing Save and Cancel buttons.
/// </summary>
public sealed class ToolbarWidget : ContainerWidget
{
    /// <summary>
    /// Initializes a new toolbar widget.
    /// </summary>
    /// <param name="renderSurfaceHost">
    /// The render surface host that owns the toolbar.
    /// </param>
    /// <param name="view">
    /// The view in which the toolbar is displayed.
    /// </param>
    /// <param name="bounds">
    /// The toolbar bounds in screen pixels.
    /// </param>
    public ToolbarWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle bounds)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            bounds.Location,
            "toolbar")
    {
        SaveButton = AddChild(
            new ButtonWidget(
                renderSurfaceHost,
                view,
                new Rectangle(
                    bounds.X + 8,
                    bounds.Y + 8,
                    100,
                    36),
                "Save",
                "toolbar.save"),
            new Vector2(
                8,
                8));

        CancelButton = AddChild(
            new ButtonWidget(
                renderSurfaceHost,
                view,
                new Rectangle(
                    bounds.X + 116,
                    bounds.Y + 8,
                    100,
                    36),
                "Cancel",
                "toolbar.cancel"),
            new Vector2(
                116,
                8));
    }

    /// <summary>
    /// Gets the Save button.
    /// </summary>
    public ButtonWidget SaveButton { get; }

    /// <summary>
    /// Gets the Cancel button.
    /// </summary>
    public ButtonWidget CancelButton { get; }
}
```

Use `AddChild(...)`, rather than calling the inherited `Add(...)` directly, for widget children. `AddChild(...)` installs the container-specific lifecycle and keyboard-bubbling behavior.

Use the inherited `Add(...)` for non-widget direct-drawing children.

---

## Creating a Draggable Widget

For a draggable leaf widget, derive from:

```csharp
DraggableWidgetBase
```

For a draggable container, derive from:

```csharp
DraggableContainerWidget
```

Both expose:

```csharp
IsDragEnabled
IsDragging
DragThresholdPx

DragStarted
Dragged
DragEnded
```

Useful overrides include:

```csharp
protected override bool CanStartDrag(
    WidgetPointerEventArgs args)
{
    return base.CanStartDrag(args);
}

protected override Vector2 ConstrainDragPosition(
    Vector2 proposedPositionPx)
{
    return proposedPositionPx;
}

protected override void OnDragStarted(
    WidgetDragEventArgs args)
{
    base.OnDragStarted(args);
}

protected override void OnDragged(
    WidgetDragEventArgs args)
{
    base.OnDragged(args);
}

protected override void OnDragEnded(
    WidgetDragEventArgs args)
{
    base.OnDragEnded(args);
}
```

### Restricting the drag area

A dialog commonly allows dragging only from its title bar:

```csharp
protected override bool CanStartDrag(
    WidgetPointerEventArgs args)
{
    return base.CanStartDrag(args) &&
           TitleBar.GetDrawLocationScreen(
               args.View)
           .Contains(
               args.ScreenPositionPx.X,
               args.ScreenPositionPx.Y);
}
```

### Constraining movement

```csharp
protected override Vector2 ConstrainDragPosition(
    Vector2 proposedPositionPx)
{
    return new Vector2(
        Math.Max(
            0,
            proposedPositionPx.X),
        Math.Max(
            0,
            proposedPositionPx.Y));
}
```

Scene-layer drag deltas are automatically converted from screen movement into the widget's world-position coordinate space.

---

## Events or Overrides?

Use overrides when behavior is intrinsic to the widget type:

```csharp
protected override void OnPointerClick(
    WidgetPointerEventArgs args)
{
    base.OnPointerClick(args);
    PerformInternalAction();
}
```

Use public events when application code should react externally:

```csharp
counter.ValueChanged += value =>
{
    gameState.CounterValue = value;
};
```

A reusable widget will often do both:

1. Perform its own internal behavior in an override.
2. Raise a semantic event such as `Clicked`, `ValueChanged`, `Closed`, or `SelectionChanged`.

Avoid requiring application code to understand low-level pointer details when the widget can expose a higher-level event.

---

## Visibility, Focus, and Input State

Hiding or disabling a widget releases active routed state.

This matters when a widget is:

- Hovered
- Capturing a pressed mouse button
- Tracking a touch contact
- Focused for keyboard input
- In the middle of a drag operation

Use the widget lifecycle rather than changing only child visibility:

```csharp
widget.Hide();
```

For draggable widgets, hiding or cancelling also resets internal drag state.

---

## Drawing Order and Input Order

These are separate systems.

### Drawing order

Set the `ZOrder` of the direct-drawing children:

```csharp
Background.ZOrder = 100;
Label.ZOrder = 101;
```

Calling the composite's `SetZOrder(...)` applies one Z-order to every descendant. For layered widget visuals, set each child's Z-order individually.

### Input order

Call:

```csharp
widget.Activate();
```

The most recently activated registered widget is considered first during overlapping hit tests.

For container widgets, children are activated after their parent so interactive children remain ahead of the container in input order.

---

## Ownership and Disposal

A widget owns children added through `Add(...)` or `AddChild(...)`.

Dispose the top-level widget when it is no longer needed:

```csharp
counter.Dispose();
```

The composite disposes its descendants recursively.

When overriding `Dispose()` to clean up additional resources:

```csharp
/// <inheritdoc/>
public override void Dispose()
{
    // Detach external subscriptions or dispose resources not owned as children.

    base.Dispose();
}
```

Do not separately dispose a child that is still owned by the widget unless the widget's design explicitly requires early child removal. Use `Remove(...)` or `RemoveChild(...)` first when ownership needs to be transferred.

---

## Common Mistakes

### Forgetting to call `Show()`

Constructing the widget creates its visuals, but `Show()` registers it for routed input.

```csharp
widget.Show();
```

### Mixing views

A view-mode widget cannot contain children assigned to different views.

```text
Widget → View A
Background → View A
Label → View B   // invalid
```

### Mixing coordinate modes

A scene-layer widget cannot contain a view-level child, and vice versa.

### Mixing scene layers

All scene-layer descendants within one composite must use the same `SceneLayer`.

### Reusing one child in multiple widgets

A child may have only one composite parent.

Remove it from the old parent before reparenting it.

### Using `Add(...)` for child widgets in a container

Use:

```csharp
AddChild(
    childWidget,
    localOffset);
```

This ensures lifecycle propagation and keyboard bubbling are configured.

### Enabling focus but not keyboard input

A keyboard-interactive widget generally requires both:

```csharp
CanReceiveFocus = true;
IsKeyboardInputEnabled = true;
```

### Confusing `Activate()` with Z-order

`Activate()` changes input priority. It does not automatically change visual Z-order.

### Consuming input without setting `Handled`

After a widget consumes an interaction:

```csharp
args.Handled = true;
```

This is particularly important for keyboard bubbling through `ContainerWidget`.

### Overriding framework processing hooks unnecessarily

Prefer `OnPointerClick`, `OnKeyboardInput`, and the other `On...` methods for custom behavior. The `Process...` hooks are used by reusable framework base classes such as draggable and container widgets.

---

## Custom Widget Checklist

Before considering a custom widget complete, verify that:

- [ ] The correct base class is used.
- [ ] All children use the same render host.
- [ ] All children use the same drawing mode.
- [ ] View-level children use the same view.
- [ ] Scene-layer children use the same scene layer.
- [ ] Child offsets are correct relative to the widget anchor.
- [ ] Child Z-orders produce the intended visual layering.
- [ ] Pointer input is enabled only when needed.
- [ ] Keyboard widgets enable focus and keyboard input.
- [ ] Consumed events set `Handled`.
- [ ] `Show()` is called before interaction is expected.
- [ ] `Activate()` is used when the widget should lead overlapping input.
- [ ] Child widgets are attached through `AddChild(...)`.
- [ ] Disposal occurs at the top-level ownership boundary.
- [ ] View and scene-layer constructors are both provided when the widget should support both coordinate spaces.

---

## Recommended Design Pattern

A reusable Gondwana widget generally follows this structure:

```text
Constructor
├── Select drawing mode and anchor
├── Create direct-drawing children
├── Add children to the composite
├── Set child Z-orders
└── Configure input and focus

Internal behavior
├── Override lifecycle hooks
├── Override input hooks
├── Update child visuals
└── Raise semantic public events

Application usage
├── Construct
├── Subscribe to semantic events
├── Show
├── Activate when appropriate
└── Dispose when finished
```

The core principle is to treat a widget as one logical interactive object whose visuals are composed from independently rendered, movable children.
