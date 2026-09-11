# Changelog

All notable changes to this project will be documented in this file.


# [Unreleased]



## Added
- Add guarded api history rebuild and validation ([#325](https://github.com/Isthimius/Gondwana/pull/325))
- Decouple backbuffer resolution from adapter presentation ([#335](https://github.com/Isthimius/Gondwana/pull/335))



## Fixed
- Improve bitmap render adapters ([#291](https://github.com/Isthimius/Gondwana/pull/291))
- Repair Linux API history encoding and pin Doxygen ([#328](https://github.com/Isthimius/Gondwana/pull/328))



## Refactoring
- Tighten touch and mouse polling behavior ([#249](https://github.com/Isthimius/Gondwana/pull/249))



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



## Added
- Add support for .gts files and tilesheets




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026




# v2.3.0 - May 20, 2026

## Added
- Introduce the Avalonia rendering adapter and GPU render-surface control
- Add poll-driven touch input with gesture recognition

## Fixed
- Correct render destination handling and dispose per-frame GL render targets
- Make the touch queue thread-safe and clarify adapter disposal ownership
- Correct gesture recognition and touch-event polling behavior
