# Slider.Shared

Platform-neutral puzzle logic, GPU sprite renderer, Gondwana.Widgets UI, and persistence models for Slider.

This is a compiled `net10.0` project. The desktop host references it with a normal `ProjectReference`; there are no linked `Compile` items.

`SliderGameRuntime` owns the game lifecycle, and platform hosts own window creation, file dialogs, audio loading, storage destinations, URI launching, and rendered-frame notifications. Hosts also provide a resize callback for logical-resolution changes.
