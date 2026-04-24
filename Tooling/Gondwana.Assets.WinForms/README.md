# Gondwana.Assets.WinForms

**Gondwana.Assets.WinForms** provides WinForms-based tooling for creating, editing, and managing Gondwana asset files.

It is intended for development workflows rather than runtime use.

## Features

- Asset file creation and editing
- Stream-based asset storage
- Zip-backed asset containers
- Designed for integration with Gondwana pipelines

## Usage

Use within tooling applications to manage asset bundles:

```csharp
var assets = new AssetsFile("assets.gaf");
assets.Add(...);
assets.Save();
```

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` — Core engine

## License

MIT
