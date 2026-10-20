# Shared authoring assets

`AuthoringResourceResolver` loads GTS, GANI and GSCN definitions and decodes GTS
images for the authoring editors without starting the engine or registering
runtime assets. `AuthoringResource` keeps the physical path, expected `AssetTypes`
and exact packed entry name separate from the logical member selected within a
multi-definition document.

The reusable `AssetPackageCatalog` now lives in this project; its
`Gondwana.Tooling.Tilesheets.Sources` namespace is retained for existing callers.
It uses detached `AssetsFile` instances and checks the exact typed entry before
reading it. Explicit packed references never fall back to loose files, another
entry with the same basename, or another logical definition.

Relative dependency paths are resolved against the physical definition's parent
directory. For a packed definition this is the archive directory, not the virtual
entry directory. A packed GTS image with an entry name and no archive path uses
the containing archive. No references are rewritten during preview loading.

Catalogs buffer packages and close file streams before returning; decoded images
are owned by their editor source and disposed when replaced. No extraction or
paint-time archive reads occur. A package's length and last-write time invalidate
its cached contents on the next load. **Reload sources** in GANI/GSCN/GSPR and
**Reload image** in GTS explicitly invalidate caches, including changes that
preserve file timestamps. Other open editors retain their existing bitmaps until
reloaded.

Hosts may supply the existing `PasswordProvider` callback. Failed attempts are
cached until invalidation or a file change so passive loads do not repeatedly
prompt. Standalone controls without a provider report inaccessible archives as
dependency warnings. Passwords are never stored in definitions.

The controls share Skia decoding, masking and owned GDI bitmap conversion through
`Tooling/Shared/WinForms/AuthoringPreviewImage.cs`. GANI keeps its existing playback
and timing; scenes use a representative first animation frame. Sprites with a
starting animation preview its first frame using that animation's own declared
GTS sources; scene/layer positioning remains unchanged.

This is dependency loading only. Opening packed definitions as editable Studio
documents and saving them back to GAF remain separate document-lifecycle work.
