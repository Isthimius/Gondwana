# SpotAvalonia

This demo targets cross-platform `net8.0`. It runs without sound unless a
compatible, byte-capable `IAudioBackend` is configured through
`AudioResourceManager.Instance.ConfigureBackend` before the game host is initialized.
Without a backend, audio assets are skipped, a warning is logged, and the window's
music/sound controls are disabled; rendering and gameplay remain available.

`Gondwana.Audio.NAudio` is Windows-only and is deliberately not referenced by
this cross-platform demo. To use it in an explicitly Windows-targeted adaptation,
reference that package and call `Engine.Instance.UseNAudio()` before host initialization.
