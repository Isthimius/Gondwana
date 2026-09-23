# Importer regression fixtures

All source XML, Godot text resources, pixels and Aseprite binary chunks in this
project are handcrafted by the test builders. They contain no third-party art
and require no installed Godot, Tiled or Aseprite application.

Tests build small source files in isolated temporary directories, analyze them
without writing, import them, and load the native outputs with Gondwana's
serializers and validators. Aseprite tests also decode the generated PNG and
check pixels, timing, direction and finite repeats. Rendering tests cover group
opacity, hidden layers and cel ordering separately from binary parsing.

The shared output pipeline tests cover collisions, missing dependencies,
cancellation, source protection and deterministic overwrite. The rollback test
uses Windows file-sharing semantics to fail a later commit after an earlier
replacement; that case runs only on Windows. The remaining headless tests are
platform-independent; Linux Skia native assets are included for Linux runners.

Run with `dotnet test Testing/Gondwana.Tooling.Importers.Tests -c Release`.
