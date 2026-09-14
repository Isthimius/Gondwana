Widgets are Gondwana’s system for **interactive, reusable interface elements built on top of DirectDrawing**.

They are intended for interface elements rendered inside the game itself rather than platform-native UI. This includes things like:

* buttons, check boxes, radio buttons, and text boxes
* labels, lists, combo boxes, and progress indicators
* panels and layout containers
* dialogs and conversation boxes
* menus
* health bars and name tags
* splash screens, notifications, popups, and other overlays

Widgets use the same Gondwana rendering pipeline as the rest of the game and can participate in either screen-space or world-space rendering.

---

## Built on DirectDrawing

A widget uses **DirectDrawing** for its visual representation, but adds the lifecycle, composition, focus, and input behavior expected from a user-interface element.

Depending on how it is associated, a widget can appear as:

* a **SceneLayer-bound** world-space element
* a **View-bound** screen-space interface element

That means the same widget system can support both in-world interaction and traditional HUD, menu, dialog, and overlay interfaces.

As a general rule:

* Use a **View** when the widget should remain screen-relative, such as menus, HUD text, settings panels, dialogs, and most conventional UI.
* Use a **SceneLayer** when the widget should exist in world coordinates and move through the scene with that layer.
* `HealthBarWidget` and `NameTagWidget` are world-space widgets designed to follow a `Sprite`.
* `ToastWidget` and `SplashScreen` are view-level overlays.

---

## WidgetBase

All widgets ultimately derive from `WidgetBase`, which provides the common widget lifecycle and interaction model.

This includes:

* showing and hiding
* activation and cancellation
* focus participation
* pointer hit testing
* pointer enter and leave events
* pointer down, up, and click events
* keyboard input
* optional drag behavior

Specialized widget base classes add behavior where it is useful.

For example, container widgets add ownership and lifecycle propagation for child widgets, while draggable widgets build on the shared pointer pipeline rather than implementing platform-specific mouse or touch handling.

Application code normally works with the concrete widget classes rather than deriving directly from `WidgetBase`.

---

## WidgetInputRouter

Interactive widget input is coordinated by `WidgetInputRouter`.

It:

* tracks widgets associated with a render surface
* determines which widget is under the pointer
* routes mouse or touch input to that widget
* manages pointer capture during clicks and drags
* manages keyboard focus
* routes keyboard input to the focused widget
* provides a platform-agnostic input path

The platform hosts translate native input into Gondwana input events. Widgets themselves do not need to know whether they are running under WinForms, Avalonia, or Blazor.

The normal `GameHostBase` initialization path creates and starts the widget input router automatically, so most Gondwana games do **not** need to create one manually.

If you are implementing a custom host, you can create the router using whatever input pollers that host provides:

```csharp
using Gondwana.Widgets;

using var widgetInputRouter = new WidgetInputRouter(
    host,
    keyboardEventPoller,
    mouseEventPoller,
    touchEventPoller);

widgetInputRouter.Start();
```

An input poller can be `null` when that input source is not available.

---

## Why Widgets Exist

DirectDrawing can render almost any custom visual, but rendering alone does not provide the lifecycle and input behavior expected from a user-interface control.

Widgets provide that higher-level structure while remaining:

* engine-native
* code-first
* platform-agnostic
* compatible with Gondwana's existing View and SceneLayer rendering models

A useful mental model is:

> **Direct drawings are engine-managed custom visuals. Widgets are interactive or reusable direct-drawing components with a shared lifecycle and input model.**

Not every widget is interactive. Display-oriented widgets such as labels, progress bars, health bars, name tags, and popups still benefit from the same composition and lifecycle model without needing direct user input.

---

# Available Widgets

The following pages cover the current widgets available in `Gondwana.Widgets`.

## Controls

### [[ButtonWidget|Widgets---ButtonWidget]]

A clickable text button for commands and actions. Supports pointer activation as well as keyboard activation with Enter or Space.

### [[CheckBoxWidget|Widgets---CheckBoxWidget]]

A two-state control for independent Boolean options such as enabling music, showing diagnostics, or toggling gameplay settings.

### [[ComboBoxWidget|Widgets---ComboBoxWidget]]

A compact single-selection control that expands into a `ListBoxWidget`. Useful for difficulty, resolution, character class, or other enumerated choices.

### [[HyperlinkWidget|Widgets---HyperlinkWidget]]

Interactive text that opens an external absolute URI through a platform-provided `IExternalUriLauncher`.

### [[LabelWidget|Widgets---LabelWidget]]

A retained-mode, non-interactive text widget for captions, headings, status displays, instructions, and other interface text.

### [[ListBoxWidget|Widgets---ListBoxWidget]]

A vertically scrolling list of strings with pointer and keyboard selection.

### [[PanelWidget|Widgets---PanelWidget]]

A rectangular container used to group other widgets. Supports solid or image backgrounds, borders, rounded corners, and child-widget positioning.

### [[ProgressBarWidget|Widgets---ProgressBarWidget]]

A generic horizontal or vertical progress indicator representing a normalized value from `0.0` through `1.0`.

### [[RadioButtonWidget|Widgets---RadioButtonWidget]]

A single-choice control that can participate in a `RadioButtonGroup` so selecting one option automatically clears the others.

### [[TextBoxWidget|Widgets---TextBoxWidget]]

A single-line editable text control with caret movement, insertion, deletion, placeholder text, length limits, and submit behavior.

---

## Layout

### [[StackPanelWidget|Widgets---StackPanelWidget]]

A lightweight layout container that arranges child widgets sequentially in a horizontal or vertical stack with configurable spacing.

---

## Dialogs and Dialogue

### [[AboutBox|Widgets---AboutBox]]

A ready-to-use application information dialog with application name, version, optional description, copyright, logo, and external hyperlink.

### [[DialogBox|Widgets---DialogBox]]

The base class for custom Gondwana dialogs. Provides a panel, title bar, optional close button, dragging, keyboard accept/cancel behavior, and `DialogResult` semantics.

### [[ConversationBox|Widgets---ConversationBox]]

An NPC-style dialogue panel with speaker name, wrapped body text, continue indicator, and pointer or keyboard advance behavior.

---

## HUD Widgets

### [[HealthBarWidget|Widgets---HealthBarWidget]]

A world-space health bar that automatically follows a `Sprite` and displays current health relative to a configurable maximum.

### [[NameTagWidget|Widgets---NameTagWidget]]

A world-space text label that automatically follows a `Sprite`, suitable for NPC names, player names, unit labels, or similar identifiers.

---

## Overlays

### [[PopupWidget|Widgets---PopupWidget]]

Short-lived text or image content that can move, accelerate, fade, and originate from view coordinates, world coordinates, a tile, or a grid cell.

Typical uses include damage numbers, healing values, XP gains, item pickups, and status effects.

### [[SplashScreen|Widgets---SplashScreen]]

A full-view splash image that fades in, holds for a minimum duration, optionally performs startup work, and then fades out.

### [[ToastWidget|Widgets---ToastWidget]]

A temporary view-level notification that can slide or fade into place, remain visible for a configured duration, and dismiss automatically or on user input.

---

## Input at a Glance

Interactive widgets such as buttons, check boxes, radio buttons, list boxes, combo boxes, text boxes, conversation boxes, dialogs, hyperlinks, and dismissible toasts participate in normal widget focus, pointer, and keyboard routing.

Display and layout widgets such as:

* `LabelWidget`
* `PanelWidget`
* `ProgressBarWidget`
* `HealthBarWidget`
* `NameTagWidget`
* `PopupWidget`
* `StackPanelWidget`

do not normally require direct input.

---

## Installation

Add the Widgets package to your game project:

```bash
dotnet add package Gondwana.Widgets
```

Then import the widget namespace or the namespace containing the widget family you are using:

```csharp
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using Gondwana.Widgets.Dialogue;
using Gondwana.Widgets.Hud;
using Gondwana.Widgets.Layout;
using Gondwana.Widgets.Overlays;
```

---

## Where to Read Next

Start with the individual widget pages above for normal game development.

For lower-level details about the shared widget system, see:

* `Gondwana.Widgets/WidgetBase.cs`
* `Gondwana.Widgets/ContainerWidget.cs`
* `Gondwana.Widgets/WidgetInputRouter.cs`
* `Gondwana.Widgets/*`

For the underlying rendering model, see the [[DirectDrawing]] documentation.
