# SpotAvalonia

SpotAvalonia is the cross-platform desktop Avalonia host for the canonical Spot! showcase.

The game no longer maintains a separate Avalonia-specific implementation. It compiles the same
`Spot.Shared` game model, host partials, Gondwana Widgets dialogs, menu, HUD, particles, and
presentation logic used by the WinForms and Blazor versions, with only the platform-specific
Avalonia host, input mapping, content loading, and window lifecycle kept here.

## Rendering

SpotAvalonia uses `AvaloniaGpuGameHost` and `AvaloniaGpuRenderSurfaceControl`, so desktop
rendering follows Gondwana's GPU/OpenGL path on supported Windows, macOS, and Linux Avalonia
backends.

## Audio

The project targets cross-platform `net8.0` and intentionally does not reference
`Gondwana.Audio.NAudio`, which is Windows-only. If no compatible desktop `IAudioBackend`
has been configured before the host initializes, Spot skips loading its audio resources and
continues normally without sound. The shared music and sound-effect settings remain safe to
use and persist, but naturally produce no playback until a backend is available.

An explicitly Windows-targeted adaptation may reference `Gondwana.Audio.NAudio` and configure
the NAudio backend before initializing the game host.
