# Changelog

All notable changes to this project will be documented in this file.


# [Unreleased]

## Fixed
- Match LibVLC RV32 BGRX output to Skia BGRA, explicitly normalize opaque alpha, and copy padded rows safely.
- Consume decoded frames through a bounded reusable mailbox on the engine update thread; handle format changes and callback shutdown.
- Publish deterministic asynchronous metadata and actual decoded source dimensions instead of nominal 1280x720 frames.
- Use inherited opacity/fades once, require constructor bounds, and clip aspect-fill presentation.
- Cancel obsolete parsing on reopen, preserve native media-input lifetimes, and restart loops outside native callbacks.

## Added
- Stream-backed playback and `VideoSource.FromAsset` for GAF video without extraction files, with explicit ownership.
- Metadata snapshots/readiness, actionable native loading errors, fake-player regression tests and an opt-in GPU/native smoke demo.

## Documentation
- Document Windows/macOS/Linux native deployment, required bounds, event threading and a clean-consumer smoke procedure.
- Clarify desktop-only scope and custom-backend BGRX compatibility requirements.

# v2.6.0 - September 17, 2026



## Added
- Add guarded api history rebuild and validation ([#325](https://github.com/Isthimius/Gondwana/pull/325))



## Fixed
- Repair Linux API history encoding and pin Doxygen ([#328](https://github.com/Isthimius/Gondwana/pull/328))



## Documentation
- Add release notes and gitlab and bitbucket mirrors ([#327](https://github.com/Isthimius/Gondwana/pull/327))
- Clarify skia and skia sharp license ([#330](https://github.com/Isthimius/Gondwana/pull/330))



## CI
- Add gitlab mirror workflow ([#326](https://github.com/Isthimius/Gondwana/pull/326))



## Maintenance
- Remove one-time job ([#329](https://github.com/Isthimius/Gondwana/pull/329))

# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026



## Added
- Implement widget input handling and routing




# v2.5.0 - July 09, 2026




# v2.4.3 - June 16, 2026




# v2.4.2 - June 11, 2026




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026




# v2.3.0 - May 20, 2026

# v2.1.0 - April 20, 2026

## Changed
- Update LibVLCSharp to 3.9.7

## Documentation
- Expand API documentation for `IVideoPlayer`, `VlcVideoPlayer`, `DirectVideo`, stretch modes, and playback events

## Packaging
- Add package-specific NuGet metadata and README
