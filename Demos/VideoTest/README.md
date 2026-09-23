# VideoTest

Opt-in Windows GPU video dogfooding demo based on the current New Project template.
Native LibVLC is supplied by the official VideoLAN.LibVLC.Windows NuGet dependency.
No media is included. See [Gondwana.Video smoke instructions](../../Gondwana.Video/README.md#reproducible-desktop-smoke-test)
for URI, stream, GAF, headless and published-consumer commands and the manual checklist.

The headless check does not validate audible output or GPU presentation. Native
log messages during demux probing/stop can be diagnostic noise; the check fails on
missing metadata/frames, loop timeout, ownership errors or a reported playback failure.
