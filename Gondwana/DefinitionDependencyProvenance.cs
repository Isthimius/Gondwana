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

    internal static DefinitionProvenance? ForAnimation(string key)
    {
        if (!Cycle._cycles.TryGetValue(key, out var cycle))
            return null;
        var provenance = cycle.DefinitionProvenance;
        return provenance is not null &&
            provenance.Matches(AnimationDefinitionSerializer.FromCycle(cycle))
                ? provenance
                : null;
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
