# HyperlinkWidget

`HyperlinkWidget` displays interactive text that opens an external absolute URI. It is useful for an About box, documentation link, project website, privacy policy, or other destination outside the game.

Unlike ordinary widgets, it needs an `IExternalUriLauncher` supplied by the host application. That keeps platform-specific URI launching outside the widget itself.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var link = new HyperlinkWidget(
    externalUriLauncher,
    host,
    view,
    new Rectangle(40, 40, 320, 30),
    "Visit the Gondwana website",
    new Uri("https://github.com/Isthimius/Gondwana"));

link.LinkOpened += uri =>
{
    Log.Information($"Opened {uri}");
};

link.LinkOpenFailed += (uri, ex) =>
{
    Log.Error(ex, $"Could not open {uri}");
};
```

The link can be activated with the primary pointer button, **Enter**, or **Space**.

## Opening a link from code

```csharp
await link.OpenAsync();
```

You can change the destination later:

```csharp
link.NavigateUri = new Uri("https://isthimius.github.io/Gondwana/");
```

Use absolute URIs.

## `IExternalUriLauncher`

Your host integration supplies the service that knows how to open a URI on the current platform. A minimal implementation looks like:

```csharp
public sealed class ExternalUriLauncher : IExternalUriLauncher
{
    public ValueTask OpenAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        // Call the platform-specific browser/URI API here.
        return ValueTask.CompletedTask;
    }
}
```

## Useful members

| Member | Purpose |
| --- | --- |
| `NavigateUri` | Destination opened on activation. |
| `OpenAsync()` | Opens the destination programmatically. |
| `LinkOpened` | Raised after a successful launch. |
| `LinkOpenFailed` | Raised if launching fails. |
| `Label` | Underlying `TextBlock` for additional visual customization. |

Both `View` and `SceneLayer` constructors are available.
