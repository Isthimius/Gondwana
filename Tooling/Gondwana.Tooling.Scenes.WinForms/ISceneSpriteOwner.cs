using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>A host's live GSPR document. The host retains ownership and controls persistence.</summary>
public interface ISceneSpriteOwner
{
    /// <summary>The current saved loose or packed identity, or null before the first save.</summary>
    AssetLocation? Location { get; }
    /// <summary>Opaque identity of the current live editing session.</summary>
    object Session { get; }
    /// <summary>Changes when the live content, member selection or save location changes.</summary>
    long Revision { get; }
    /// <summary>The live members, including unsaved edits.</summary>
    IReadOnlyList<SpriteDefinition> Definitions { get; }
    /// <summary>Whether the host currently permits edits to the live owner.</summary>
    bool CanPlace { get; }
    /// <summary>The owning document's Undo and Redo history.</summary>
    EditHistory History { get; }
    /// <summary>Commits pending property editor changes before a gesture or history command.</summary>
    bool CommitEdits();
    /// <summary>Selects the requested instance in its own editor without activating another tab.</summary>
    void Select(string member, Guid id);
    /// <summary>Commits a placement through the owning document's normal authoring operations.</summary>
    bool Place(SpriteInstanceDrag source, ResourceAddress scene, string layerId, PointF position);
}
