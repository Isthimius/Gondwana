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

Malformed or conflicting physical reference fields fail with member-qualified diagnostics. Duplicate mapping sources and chains/cycles such as A â†’ B plus B â†’ C are rejected because repeated application would be ambiguous. No part of the input document is changed on failure. The API validates reference structure, not resource existence, target document membership, frame content, or playback data; callers may run the existing format validators separately, including when working with incomplete authoring drafts.

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

This layer does not implement semantic pack/unpack, GAF editor import, project-wide inbound-reference discovery, rename/move orchestration, or reference-aware deletion. It does not append to existing destination documents or verify that a later physical move has happened.
