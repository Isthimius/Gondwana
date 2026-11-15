using System.Text.Json;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Uses host dependency metadata, never filenames in an application directory, to identify framework assets.</summary>
internal static class FrameworkAssemblyCatalog
{
    private static readonly string[] Frameworks = ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App", "Microsoft.AspNetCore.App"];

    internal static HashSet<string> ReadRuntime()
        => Read(((string?)AppContext.GetData("APP_CONTEXT_DEPS_FILES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries));

    internal static HashSet<string> Read(IEnumerable<string> dependencyFiles)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in dependencyFiles)
        {
            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);
            var frameworkManifest = Frameworks.Any(framework =>
                Path.GetFileName(file).Equals(framework + ".deps.json", StringComparison.OrdinalIgnoreCase));
            if (!document.RootElement.TryGetProperty("targets", out var targets)) continue;
            foreach (var target in targets.EnumerateObject())
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    // A self-contained application's deps file also contains private app libraries.
                    // Only SDK-generated framework runtime-pack sections authorize shared fallback.
                    if (!frameworkManifest && !Frameworks.Any(framework =>
                        library.Name.StartsWith("runtimepack." + framework + ".Runtime.", StringComparison.OrdinalIgnoreCase))) continue;
                    if (!library.Value.TryGetProperty("runtime", out var runtime)) continue;
                    foreach (var asset in runtime.EnumerateObject())
                        if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            names.Add(Path.GetFileNameWithoutExtension(asset.Name));
                }
            }
        }
        return names;
    }
}
