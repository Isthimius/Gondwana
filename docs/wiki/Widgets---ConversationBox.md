# ConversationBox

`ConversationBox` is an NPC-style dialogue panel with a speaker name, wrapped body text, a continue indicator, and built-in pointer/keyboard advance behavior.

Use it for NPC conversations, narration boxes, tutorials, quest dialogue, and similar game text.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Dialogue;

var conversation = new ConversationBox(
    host,
    view,
    new Rectangle(40, 300, 560, 150),
    speaker: "Guard",
    text: "Halt! State your business.");

conversation.AdvanceRequested += () =>
{
    ShowNextLine();
};
```

The player can request the next step by clicking the box, pressing **Enter**, or pressing **Space**.

## Updating dialogue

```csharp
conversation.SetConversation(
    "Guard",
    "Very well. You may pass.");

// Or independently:
conversation.SetSpeaker("Merchant");
conversation.SetText("Take a look at my wares.");
```

## Continue indicator

```csharp
conversation.ShowContinueIndicator(false);

// Show it again:
conversation.ShowContinueIndicator();
```

## Custom advance keys

```csharp
conversation.PrimaryAdvanceKey = 13;
conversation.SecondaryAdvanceKey = 32;
```

## Programmatic advance

```csharp
conversation.Advance();
```

This raises `AdvanceRequested` just as user input does.

## Styling

```csharp
conversation
    .SetTextColors(
        SkiaSharp.SKColors.Gold,
        SkiaSharp.SKColors.White)
    .SetPanelColors(
        Color.FromArgb(238, 24, 25, 32),
        Color.White)
    .SetConversationZOrder(500);
```

## View or SceneLayer

A conversation box can be attached to a `View` for conventional screen-space dialogue or to a `SceneLayer` for world-space presentation.

## Useful members

| Member | Purpose |
| --- | --- |
| `Speaker` / `Text` | Current displayed dialogue. |
| `AdvanceRequested` | Raised when the player requests the next step. |
| `SetConversation()` | Updates speaker and body together. |
| `ShowContinueIndicator()` | Controls the small continue marker. |
| `Panel`, `SpeakerText`, `BodyText`, `ContinueIndicator` | Underlying visuals. |
