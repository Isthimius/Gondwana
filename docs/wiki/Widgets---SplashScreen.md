# SplashScreen

`SplashScreen` displays a full-view image that fades in, remains visible for a minimum hold period, and fades out. It can also perform synchronous or asynchronous startup work during the hold phase.

Use it for studio logos, game logos, startup branding, or a loading/startup screen.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

`SplashScreen` is created with `TryCreate()` rather than `new`:

```csharp
using Gondwana.Widgets.Overlays;

using Stream splashStream =
    File.OpenRead("Assets/splash.png");

SplashScreen? splash = SplashScreen.TryCreate(
    splashStream,
    host,
    view);
```

Creation returns `null` if a usable splash cannot be created, such as when the host has no views or the image cannot be decoded.

The sequence starts automatically when the splash is created.

## Timing

```csharp
SplashScreen? splash = SplashScreen.TryCreate(
    splashStream,
    host,
    view,
    fadeInSec: 0.4f,
    holdSec: 2.0f,
    fadeOutSec: 0.4f);
```

The lifecycle is:

1. `FadingIn`
2. `Holding`
3. `FadingOut`
4. `Hidden`

You can inspect `CurrentState` while it is active.

## Running startup work

```csharp
SplashScreen? splash = SplashScreen.TryCreate(
    splashStream,
    host,
    view,
    holdSec: 1.5f,
    onHoldingSync: () =>
    {
        WarmCaches();
    },
    onHoldingAsync: async () =>
    {
        await LoadPlayerProfileAsync();
    },
    onSplashCompleted: () =>
    {
        ShowMainMenu();
    });
```

The splash remains in the holding phase until **both** conditions are satisfied:

- the configured minimum hold duration has elapsed; and
- the supplied startup work has completed.

This makes it useful for startup work without making a fast load produce a one-frame logo flash.

## Image behavior

The decoded splash image is displayed with `DirectImage.ScaleMode.Fit` and uses the target view's viewport dimensions.

The splash disposes itself after the fade-out completes.

## Useful members

| Member | Purpose |
| --- | --- |
| `TryCreate()` | Creates and immediately starts the splash sequence. |
| `Image` | Underlying `DirectImage`. |
| `CurrentState` | Current splash lifecycle state. |
| `FadeInSec` / `HoldSec` / `FadeOutSec` | Configured durations. |
