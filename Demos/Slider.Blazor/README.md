# Slider.Blazor

WebAssembly / WebGL host for the Gondwana Slider demo. All puzzle rules, sprites,
menus, layout, and save-file encoding are compiled in `../Slider.Shared`; this
project contains only the browser host, input mapping, static assets and web UI.

## Run

Requires the .NET 10 SDK and matching WebAssembly workload:

```sh
dotnet workload install wasm-tools
dotnet run --project Demos/Slider.Blazor/Slider.Blazor.csproj
```

The browser game provides the same in-engine Gondwana.Widgets sidebar/menu as
Slider.WinForms. Click or tap an adjacent tile to move it; use the arrow keys
with the canvas focused. Hold Shift to show each piece's *solved* position.
The browser's native file chooser is used for image and saved-game imports.

- **Open Picture** selects an image (16 MiB maximum).
- **Save Puzzle** downloads a compatible `.slider.json` file, including the image.
- **Load Puzzle** reads a WinForms or Blazor `.slider.json` file (64 MiB maximum).
  Exports larger than 64 MiB are rejected instead of producing unloadable files.
- **High Scores** persist locally in the current browser's `localStorage`.
- Audio is played by `Gondwana.Audio.Browser`; browser autoplay policies
  may defer playback until the first user interaction.

This is a Blazor WebAssembly app; no server is required for gameplay. The
browser chooses its own file download location. Browser storage may be cleared
by private/incognito sessions or site-data deletion. Resize is handled by a
`ResizeObserver` and Gondwana's dynamic logical-resolution request. The game
preserves a 1200x780 minimum logical canvas and scales to narrow or short
viewports so the fixed-position sidebar remains fully visible.
