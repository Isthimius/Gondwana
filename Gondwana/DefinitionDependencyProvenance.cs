using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;

namespace Gondwana;

/// <summary>
/// Resolves an existing, unchanged physical definition source for a runtime dependency.
/// A logical reference alone is not evidence that the original source still describes
/// the currently registered object.
/// </summary>
internal static class DefinitionDependencyProvenance
{
    internal static DefinitionProvenance? ForTilesheet(Tilesheet? tilesheet)
    {
        var provenance = tilesheet?.DefinitionProvenance;
        return provenance is not null &&
            provenance.Matches(TilesheetDefinitionSerializer.FromTilesheet(tilesheet!))
                ? provenance
                : null;
    }

    /// <summary>
    /// Captures the registered animation and its transitive NextCycle dependencies.
    /// Tile and sprite animators hold playback clones; follow the canonical registry
    /// instead so transitions that are not directly assigned are also exported.
    /// </summary>
    internal static IEnumerable<(string Key, DefinitionProvenance Source)> ForAnimationChain(
        string key, HashSet<string> visited)
    {
        var nextKey = key;
        while (!string.IsNullOrWhiteSpace(nextKey) && visited.Add(nextKey))
        {
            if (!Cycle._cycles.TryGetValue(nextKey, out var cycle))
                yield break;

            var provenance = cycle.DefinitionProvenance;
            if (provenance is not null &&
                provenance.Matches(AnimationDefinitionSerializer.FromCycle(cycle)))
                yield return (cycle.CycleKey, provenance);

            // Keep traversing when this cycle lacks usable provenance: a later
            // transition can still have an independently persisted definition.
            nextKey = cycle.NextCycle?.CycleKey;
        }
    }

    internal static DefinitionProvenance? ForScene(Scene? scene)
    {
        var provenance = scene?.DefinitionProvenance;
        return provenance is not null &&
            provenance.Matches(SceneDefinitionSerializer.FromScene(scene!))
                ? provenance
                : null;
    }
}
