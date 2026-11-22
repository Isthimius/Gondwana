# Changelog

All notable changes to this project will be documented in this file.

# [Unreleased]

## Refactoring
- Extract `SliderGameRuntime` as the platform-neutral game lifecycle, with injected storage, URI launching, and logical resize callbacks.
- Extract the Slider board model, sprite renderer, image layout, widgets, menu, and persistence contracts into a compiled shared assembly consumed through project references.
