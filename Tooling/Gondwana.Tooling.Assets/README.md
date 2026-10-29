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
prompt. GANI/GSCN/GSPR/GSND standalone controls create a shared password-prompt
implementation with in-memory credential reuse when no host catalog is supplied.
`PasswordAccepted` reports only successfully authenticated
credentials so a host can reuse them across dependency reloads and document opens.
Passwords are never stored in definitions.

`GetResources` lists exact physical entry identities filtered by stored `AssetTypes`.
`PackedResourceSelection` keeps the logical member separate, enumerates members
through existing serializers, and validates the selected member before application.
The single linked `Tooling/Shared/WinForms/PackedResourcePicker.cs` supplies the
GANI/GSCN/GSPR/GSND dependency UI, including **Reload / retry** for cached failures
or changed packages. Browsing never extracts or rewrites archives. Conflicting
logical names are rejected instead of silently switching an existing dependency.

The controls share Skia decoding, masking and owned GDI bitmap conversion through
`Tooling/Shared/WinForms/AuthoringPreviewImage.cs`. GANI keeps its existing playback
and timing; scenes use a representative first animation frame. Sprites with a
starting animation preview its first frame using that animation's own declared
GTS sources; scene/layer positioning remains unchanged.

This library resolves dependencies. [Studio](../Gondwana.Tooling.Studio.WinForms/README.md#editing-packed-definitions)
owns opening and editing packed native documents, coordinated transactional GAF
saves, conflict handling, and session restoration. It uses the same resolver for
their previews. Standalone editors share the preview controls but retain their
loose-document file dialogs.

## Document editing history

`Gondwana.Tooling.Editing.EditHistory` is UI- and format-independent. GTS and GSCN
own separate histories; other editors do not yet participate. `IEditCommand`
provides a description, `Execute`, and `Undo`; delegate commands and `Set` cover
simple assignments. Commands must touch only their owning model and retain model
objects, never controls. `Undo` must also compensate a partially failed `Execute`,
and the inverse applies when restoring a failed undo. Recording during execution
is rejected. An unrecoverable compensation error discards history and leaves the
document dirty rather than claiming an exact state.

```csharp
using var edit = document.History.BeginTransaction("Change cells");
document.EditTile(layer, 0, 0, "Set animation", tile => tile.AnimationKey = "water");
document.EditTile(layer, 1, 0, "Set animation", tile => tile.AnimationKey = "water");
edit.Commit(); // Dispose without Commit rolls back; nested groups close in LIFO order.
```

Commands run immediately inside a transaction; the outer commit/cancel raises one
`Changed` event. Empty groups and unchanged property assignments create no undo
entry. `MarkSaved` records a stable state identity without clearing commands;
branching and pruning cannot accidentally reuse a saved identity. Hosts call it
only after persistence succeeds. Saves are not allowed during an open transaction.
`MarkUntracked` invalidates legacy history after direct mutations; supported editor
paths use reversible operations instead.

The configurable `Limit` defaults to 200 committed operations (a group counts as
one). It is a command-count bound, not a byte quota: a large collection edit retains
its affected objects. Pruning, divergent edits, `Clear`, and `Dispose` release
retained command references. History is owned and used on the editor thread.

`ModelEdits` captures scalar/replaced properties on one affected object, retaining
only changed fields. Callers must not mutate child collections through that API.
`ModelSnapshot` is reserved for affected modal collection editors, whose objects
can change in place: it temporarily captures that collection and records only
changed fields and membership. Neither mechanism serializes a whole document.
`ReferenceOrigins` uses weak keys to preserve the path base of dormant references
retained by undo across Save As. Logical instances and the current save location
are preserved during replay. These operations are suitable for future grouped
scene painting; no painting or cross-document history is implemented here.
