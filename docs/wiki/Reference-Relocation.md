# Reference relocation

`DefinitionReferenceRelocator` is a UI-independent transformation API for complete GTS, GANI, GSCN, GSPR, and GSND documents. It returns an independent document and does not write files, open archives, move assets, or change runtime registries.

## Physical endpoints and logical members

`AssetLocation` describes a typed physical endpoint: an absolute loose path, or an absolute GAF path plus an entry name. It is an API argument, not a new persisted reference format. `ReferenceRelocation` pairs old and new endpoints of the same `AssetTypes` value.

All four transitions are supported: loose to loose, loose to packed, packed to loose, and packed to packed. The transformer updates the existing source model's path, entry name, and storage kind together. Logical selectors remain unchanged, including tilesheet names, animation keys, scene IDs, sprite layer IDs, and frame coordinates. GANI `NextCycleKey` is preserved without traversal or sorting.

```csharp
using Gondwana;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;

var sourcePath = Path.GetFullPath("Content/terrain.gts");
var destinationPath = Path.GetFullPath("Output/terrain.gts");
var document = TilesheetDefinitionSerializer.LoadDocument(sourcePath);

var transformed = DefinitionReferenceRelocator.Relocate(
    document,
    new AssetLocation(AssetTypes.TilesheetDefinition, sourcePath),
    new AssetLocation(AssetTypes.TilesheetDefinition, destinationPath),
    new[]
    {
        new ReferenceRelocation(
            new AssetLocation(AssetTypes.Image, Path.GetFullPath("Content/terrain.png")),
            new AssetLocation(AssetTypes.Image, Path.GetFullPath("Output/art.gaf"), "terrain.png"))
    });

// A later workflow must create the referenced GAF entry and coordinate its commit.
// This explicitly saves only the transformed definition document.
TilesheetDefinitionSerializer.SaveDocument(destinationPath, transformed);
```

Each format has a typed `Relocate` overload. All members are cloned and retained. Only matching physical dependencies are relocated. When the containing document moves, other dependencies are rebased to keep their original targets. When neither a reference nor its containing document changes location, its stored spelling is retained.

| Document | Transformed dependencies |
| --- | --- |
| GTS | Images, including implicit images in the containing GAF |
| GSND | Loose and packed audio; URI sources are left unchanged |
| GANI | GTS sources |
| GSCN | GTS and GANI sources |
| GSPR | GTS, GSCN, and GANI sources |

Packed audio retains an existing format hint or infers one from the original filename/entry when needed. An extensionless packed destination fails if no format can be determined.

## Validation and repeatability

Endpoints require absolute paths and compatible asset types. Document identity validation uses the existing serializers and their identity comparison rules. GAF entry comparison is case-insensitive, matching `AssetsFile`; filesystem path comparison follows the host platform. Logical identities are never used as physical filenames.

Malformed or conflicting physical reference fields fail with member-qualified diagnostics. Duplicate mapping sources and chains/cycles such as A → B plus B → C are rejected because repeated application would be ambiguous. No part of the input document is changed on failure. The API validates reference structure, not resource existence, target document membership, frame content, or playback data; callers may run the existing format validators separately, including when working with incomplete authoring drafts.

By default, every supplied mapping must match an original reference or an already-relocated destination. Otherwise the call reports the missing source. Set `requireAllMappings: false` to apply a larger batch to individual documents; references without mappings remain unchanged. On repeated application, pass the returned document's destination as its current source location. Already-relocated references are accepted, making that operation idempotent.

## Dependency paths and provenance

Dependency paths resolve against the physical document's directory, or the containing GAF's directory for packed documents. The GAF entry's name is not a filesystem directory. Rewritten paths are relative to the destination document where possible; paths on another filesystem root remain absolute. No process working directory is used to resolve relocation endpoints or document-relative dependencies.

Authoring dependency locations, serialized self-origin metadata, and runtime provenance have separate roles:

- Source records retain their logical selector plus their physical dependency location.
- Canonical `ToDocumentJson`, `SaveDocument`, `SaveAll`, and singleton `Save` omit redundant loose/packed root `Source` locations. Generated authoring markers retain their existing behavior. Raw member `ToJson` remains a compatibility projection and can include legacy source metadata.
- Loading reconstructs physical source information from the actual file or GAF. Historical absolute dependency and source paths remain readable; stale persisted self locations do not override the actual loaded location. GTS retains its historical Generated authoring marker, while its load stamp records the actual source.
- Transformed copies have destination authoring metadata but no load stamp. Only reloading committed content establishes runtime provenance eligible for `DefinitionPersistence.PreserveSource`. Member-specific baselines and logical member identities remain unchanged in that mechanism.

For unstamped authoring definitions, Save As can use fully qualified source metadata as a base; relative self-origin metadata is not resolved against the process working directory.

All five serializers perform atomic file replacement using the final destination for reference calculations. Their internal staging filenames are never serialized. Standalone editors and Studio use that same save path. Save As retains every member and does not mutate input definitions.

## Shared tooling and safety

The Core transformer above only transforms documents. `Gondwana.Tooling.Assets` coordinates physical workflows without depending on CLI or WinForms:

| Component | Responsibility |
| --- | --- |
| `PackDefinitionDocument` | Typed document access through Core serializers |
| `SemanticPackPlan` | Packaging inventory and dependency resolution, also reused by import/export |
| `SemanticUnpackPlan` | Copy/export selection, semantic preparation, staged loose output |
| `AssetImportPlan` | Prepared additions to a borrowed, current in-memory archive |
| `DefinitionReferenceDiscovery` | Exact typed inbound references across every member in the declared scope |
| `AssetRelocationPlan` / `AssetDeletionPlan` | Side-effect-free plans and reviewed physical operations |
| `AssetFileTransaction` | Shared move/delete staging, snapshot checks, recovery and rollback |
| `AssetArchiveCopy` | Archive copy with outbound rebasing; no inbound rewriting |
| `AssetArchiveFormat`, `AssetTypeMap`, `SafeFilePath` | Archive recognition, native/leaf inference, safe output paths |

`AssetsFile` remains a typed binary container. CLI commands and the shared WinForms editor adapt these services; Studio hosts the same editor. See [Assets Files](Assets-Files#physical-content-and-operations) for copy versus move and safe versus forced delete.

### Scope and credentials

Move/delete scope is one GAF/ZIP or an explicit recursive directory. Discovery includes every native document member, excluding generated directories such as `bin`, `obj`, `.git`, `.vs`, and `node_modules`. Selected sources and affected archives are also inspected. The preview identifies the actual inspected files. Outside-scope references and arbitrary game code are unknown, not proven absent. Links/junctions, inaccessible archives, malformed definitions, ambiguous selectors, and incomplete scans fail preflight. No basename guessing is used.

Each scanned archive needs its own credential. Move/delete CLI commands accept an absolute-path/password JSON map; desktop prompts request individual missing credentials. Pack/unpack currently use their single supplied password for external archives. These are distinct credential workflows, not an assumption that every archive shares a password.

Save the active archive and close other authoring documents before desktop Move/Delete. Whole-archive operations require closing that archive too. CLI cannot inspect other processes' editors; save/close them before committing. Add/Import instead uses the current in-memory archive and leaves changes unsaved. Canceling review does not mark a document dirty.

### Transactions and recovery

Planning is side-effect-free and snapshots every inspected physical file. Before commit, scope inventory and bytes are checked again; changes require a new plan. Changed loose files and archives are staged beside their final destinations, with independent original backups. Destinations and reference updates are installed before source retirement. Archives temporarily retain moved-out source entries until all destination/reference writes have succeeded; retirement then installs their final contents. Committed bytes are checked against the validated prepared graph. Ordinary exceptions and cancellation restore previously touched files, including failures during later archive installs or source retirement. Sources are never removed by discovery or staging.

This is **not** a globally atomic filesystem transaction across process crashes or power loss. Do not run concurrent writers during a commit. Interrupted commits retain `.gondwana-relocate-<id>.json` in the scope directory (or beside a single-archive scope), plus sibling `.old`, `.new`, and optional `.new.final` files. Stop writers before manual recovery. For a `Prepared` manifest, restore every `Existed: true` target from its listed backup and remove targets marked `Existed: false`; keep backups until every restoration succeeds. The `Completed` record is staged beside the `Prepared` manifest and atomically replaces it; the only recovery record is never truncated. A `Completed` manifest means the operation succeeded and leftover staging/backups can be removed. Staging abandoned before a manifest exists has not installed any writes. Rollback failures retain the manifest/backups and report their location. Filesystem corruption or lost backup data requires external backup recovery.


Deletion uses the same transaction. Internal references owned by documents also being deleted do not block; surviving inbound references do. Force authorizes only the reviewed dangling edges and leaves their owning documents unchanged. It cannot bypass incomplete scans or unrelated invalid content. Deleting the last entry retains the container. Subsequent operations may reject a graph containing deliberately broken references until the author fixes them.

Copy/export uses staged output and backups for ordinary rollback; it does not claim the move/delete recovery-manifest protocol. Import rolls back prepared in-memory additions without saving. None of these operations claims global crash atomicity.

### Intentional limitations

Physical operations treat a complete document as one unit. They do not rename/delete individual logical members, rewrite arbitrary C#, infer application-specific relationships, cascade deletion, remove orphaned assets, or garbage-collect unused content. Editor root renaming is not a project-wide reference-aware logical rename. Coordinating several dirty editor models and automatic crash recovery remain separate future work. Recovery is manual as described above; concurrent external writers must be stopped.

Useful follow-ups are automated recovery review, coordinated dirty-document operations, and richer cross-archive credential UX. They are not prerequisites for the supported scoped workflows and are not implemented here.
