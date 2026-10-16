using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

internal sealed partial class SemanticPackPlan
{
    private Func<AssetLocation?, AssetTypes, string?, string, Entry>? _referenceResolver;
    private string _referenceMember = "";

    /// <summary>Uses the existing typed format handlers to resolve and report physical dependencies.</summary>
    /// <param name="owner">Containing document location.</param>
    /// <param name="bytes">Original bytes.</param>
    /// <param name="resolve">Exact physical or unique logical resolver.</param>
    /// <param name="observe">Receives each resolved dependency.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>The validated authoring document with inferred source metadata, if native.</returns>
    internal static PackDefinitionDocument? Discover(AssetLocation owner, byte[] bytes,
        Func<AssetLocation?, AssetTypes, string?, (AssetLocation Location, byte[] Bytes)> resolve,
        Action<DefinitionReference> observe, CancellationToken cancellation)
    {
        var plan = new SemanticPackPlan(Path.GetDirectoryName(owner.Path)!, owner.Path, null, false, true, cancellation);
        var entry = ReadEntry(owner, owner, bytes);
        entry.Document?.Validate(owner.ToString());
        plan._referenceResolver = (location, type, selector, label) =>
        {
            try
            {
                var resolved = resolve(location, type, selector);
                var target = ReadEntry(resolved.Location, resolved.Location, resolved.Bytes);
                target.Document?.Validate(resolved.Location.ToString());
                if (selector is not null && !Contains(target, selector))
                    throw new InvalidDataException($"Missing logical member '{selector}' in {resolved.Location}.");
                observe(new(owner, plan._referenceMember, resolved.Location, selector, label));
                return target;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            { throw new InvalidDataException($"{label}: {ex.Message}", ex); }
        };
        plan.Analyze(entry);
        return entry.Document;
    }
}
