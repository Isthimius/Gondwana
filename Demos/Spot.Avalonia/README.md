# Spot.Avalonia

Spot.Avalonia is the Avalonia desktop host for the canonical **Spot!** demo.

The game itself is not a separate Avalonia fork. It references
[`Spot.Shared`](../Spot.Shared/) as a normal project dependency. That assembly contains
the platform-neutral Spot game model, Widget dialogs, menu/About construction, settings
helpers, and `SpotGameRuntime` used by the WinForms, Blazor, and Avalonia hosts.

Platform-specific code is intentionally limited to:

- `GameWindow`, the thin Avalonia desktop shell.
- `SpotGameHost`, a thin adapter over `SpotGameRuntime` that derives from
  `AvaloniaGpuGameHost` and supplies Avalonia keyboard/text-input translation.
- `SpotGameHost.Content`, which loads the shared GAF package and treats audio as
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

## Shared assets

All three Spot hosts package `../Spot.Shared/assets` with `Spot.Assets.targets`.
The generated `assets/spot.gaf` contains runtime images, audio, fonts, and the
`spot_defaults.gts` / `spot_selected.gts` tilesheet definitions. The definitions
reference their PNG entries in the same package and preserve the five vertical
93x96 default frames and five horizontal 64x64 selected frames. Archived assets
are excluded. Desktop output needs only the GAF; the WinForms application icon
is a build input and Blazor's loading icon remains a loose bootstrap resource.
