# TextBoxWidget

`TextBoxWidget` is a single-line editable text control with a visible caret, placeholder text, keyboard navigation, deletion, submit behavior, and a small widget-local held-key repeat throttle.

Use it for player names, chat-entry fields, search/filter text, seed values, server addresses, and other short text input.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var playerName = new TextBoxWidget(
    host,
    view,
    new Rectangle(40, 40, 280, 34),
    placeholder: "Player name");

playerName.MaxLength = 24;

playerName.TextChanged += text =>
{
    settings.PlayerName = text;
};

playerName.Submitted += text =>
{
    BeginGame(text);
};
```

By default, **Enter** submits the current text.

## Editing from code

```csharp
playerName.SetText("Ada");

playerName.CaretIndex = 1;
playerName.InsertText("l");

playerName.Backspace();
playerName.Delete();
playerName.MoveCaret(1);
```

## Read-only mode

```csharp
playerName.IsReadOnly = true;
```

The text remains visible and focusable, but user editing is disabled.

## Placeholder and limits

```csharp
playerName.Placeholder = "Enter your name";
playerName.MaxLength = 20;
```

## Host-specific keyboard mapping

The default printable-character resolver and special key values follow Windows virtual-key conventions. Other hosts can replace the resolver and key codes without subclassing:

```csharp
playerName.CharacterResolver = args =>
{
    // Translate the host key event into a printable character.
    return ResolveCharacterForThisHost(args);
};

playerName.SubmitKey = mySubmitKey;
playerName.BackspaceKey = myBackspaceKey;
```

Setting `CharacterResolver` to `null` suppresses printable character conversion.

## Useful members

| Member | Purpose |
| --- | --- |
| `Text` | Current single-line value. |
| `Placeholder` | Text shown when empty and unfocused. |
| `CaretIndex` | Current insertion position. |
| `MaxLength` | Optional maximum length. |
| `IsReadOnly` | Disables user editing. |
| `RepeatedKeyIntervalSeconds` | Minimum interval between processed held-key repeats; pressed keys remain immediate. |
| `TextChanged` | Raised when text changes. |
| `Submitted` | Raised when the submit key is pressed. |
| `SetColors()` | Sets background, normal border, and focused border colors. |
| `SetTextColors()` | Sets text and placeholder colors. |

`TextBoxWidget` supports both `View` and `SceneLayer` placement.
