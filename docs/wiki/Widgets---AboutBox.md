# AboutBox

`AboutBox` is the built-in application-information dialog. It displays an application name, version, optional description and copyright text, optional logo, and optional external hyperlink.

It is view-level UI and centers itself in the target view when explicit bounds are not supplied.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using Gondwana.Widgets.Dialogs;

var about = new AboutBox(
    host,
    view,
    applicationName: "My Game",
    version: "1.0.0",
    description: "A Gondwana-powered adventure.",
    copyright: "Copyright © 2026 Hidden Worlds Games");

about.Show();
about.Activate();
```

The built-in **OK** and close buttons close the dialog. Enter also accepts it, and Escape cancels it.

## With a logo

```csharp
using SkiaSharp;

SKImage logo = LoadLogo();

var about = new AboutBox(
    host,
    view,
    "My Game",
    "1.0.0",
    description: "A Gondwana-powered adventure.",
    logo: logo);
```

The caller retains ownership of the supplied logo image.

## With a hyperlink

```csharp
var about = new AboutBox(
    host,
    view,
    applicationName: "My Game",
    version: "1.0.0",
    uriLauncher: externalUriLauncher,
    hyperlinkUri: new Uri("https://example.com"),
    hyperlinkText: "example.com");
```

When configuring a hyperlink, both the URI launcher and an absolute URI are required.

## Handling close results

```csharp
about.Closed += result =>
{
    Console.WriteLine($"About box closed with {result}");
};
```

## Custom bounds

```csharp
var about = new AboutBox(
    host,
    view,
    "My Game",
    "1.0.0",
    bounds: new System.Drawing.Rectangle(100, 80, 560, 360));
```

If bounds are omitted, `AboutBox` uses a conventional default size constrained to the view and centers itself.

## Useful members

| Member | Purpose |
| --- | --- |
| `ApplicationName` / `Version` | Displayed application identity. |
| `Description` / `Copyright` | Optional detail text. |
| `Logo` | Optional `DirectImage`. |
| `Hyperlink` | Optional `HyperlinkWidget`. |
| `OkButton` | Built-in OK button. |
| `HeaderText`, `VersionText`, `DetailsText` | Underlying text blocks. |
