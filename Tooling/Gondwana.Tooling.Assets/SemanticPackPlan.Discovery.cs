using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

internal sealed partial class SemanticPackPlan
{
    private Func<AssetLocation?, AssetTypes, string?, string, Entry>? _referenceResolver;
    private string _referenceMember = "";
    private Action<Exception>? _discoveryProblem;
    private Action<string, string, string>? _externalReference;

    /// <summary>Uses the existing typed format handlers to resolve and report physical dependencies.</summary>
    /// <param name="owner">Containing document location.</param>
    /// <param name="bytes">Original bytes.</param>
    /// <param name="resolve">Exact physical or unique logical resolver.</param>
    /// <param name="observe">Receives each resolved dependency.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <param name="failed">Optional diagnostic receiver; allows inspection to continue at the next member.</param>
    /// <param name="external">Optional observer for URI references, which are never fetched.</param>
    /// <returns>The validated authoring document with inferred source metadata, if native.</returns>
    internal static PackDefinitionDocument? Discover(AssetLocation owner, byte[] bytes,
        Func<AssetLocation?, AssetTypes, string?, (AssetLocation Location, byte[] Bytes)> resolve,
        Action<DefinitionReference> observe, CancellationToken cancellation,
        Action<DefinitionReference?, Exception>? failed = null, Action<string, string, string>? external = null)
    {
        var plan = new SemanticPackPlan(Path.GetDirectoryName(owner.Path)!, owner.Path, null, false, true, cancellation);
        var entry = ReadEntry(owner, owner, bytes);
        entry.Document?.Validate(owner.ToString());
        if (failed is not null) plan._discoveryProblem = error => failed(null, error);
        plan._externalReference = external;
        plan._referenceResolver = (location, type, selector, label) =>
        {
            DefinitionReference? reference = location is null ? null : new(owner, plan._referenceMember, location, selector, label);
            try
            {
                var resolved = resolve(location, type, selector);
                reference = new(owner, plan._referenceMember, resolved.Location, selector, label);
                var target = ReadEntry(resolved.Location, resolved.Location, resolved.Bytes);
                target.Document?.Validate(resolved.Location.ToString());
                if (selector is not null && !Contains(target, selector))
                    throw new ReferenceInspectionException(ReferenceProblemKind.Missing, $"Missing logical member '{selector}' in {resolved.Location}.");
                observe(new(owner, plan._referenceMember, resolved.Location, selector, label));
                return target;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed?.Invoke(reference, ex);
                throw new InvalidDataException($"{label}: {ex.Message}", ex);
            }
        };
        plan.Analyze(entry);
        return entry.Document;
    }
}
