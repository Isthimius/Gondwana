using System.Runtime.CompilerServices;
using Gondwana.Tooling.Tilesheets.Sources;

// Keep existing hosts compatible with the catalog's move to the shared tooling layer.
[assembly: TypeForwardedTo(typeof(AssetPackageCatalog))]
[assembly: TypeForwardedTo(typeof(PackedImageSource))]
