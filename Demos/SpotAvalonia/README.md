# SpotAvalonia

SpotAvalonia is the Avalonia desktop host for the canonical **Spot!** demo.

The game itself is not a separate Avalonia fork. It shares the current
[`Spot.Shared`](../Spot.Shared/) game model, Widget dialogs, menu/About construction,
settings helpers, and host partials with the WinForms and Blazor versions.

Platform-specific code is intentionally limited to:

- `GameWindow`, the thin Avalonia desktop shell.
- `SpotGameHost`, which derives from `AvaloniaGpuGameHost` and supplies Avalonia
  keyboard/text-input translation.
- `SpotGameHost.Content`, which loads the desktop asset files and treats audio as
  optional when no compatible `IAudioBackend` is configured.

Rendering uses `AvaloniaGpuRenderSurfaceControl`, matching the GPU-only direction of
the current WinForms Spot demo. The in-engine `MenuBarWidget`, New Game dialog,
How-to-Play dialog, About box, splash presentation, HUD, particles, gameplay, and
settings behavior all come from the same shared implementation.

## Audio

This project targets cross-platform `net8.0`, so it does not reference the
Windows-only `Gondwana.Audio.NAudio` backend. When no compatible audio backend has
been configured, Spot still runs normally without sound and the Music and Sound
Effects menu items are disabled.

An explicitly Windows-targeted adaptation can reference `Gondwana.Audio.NAudio` and
configure it before host initialization.
