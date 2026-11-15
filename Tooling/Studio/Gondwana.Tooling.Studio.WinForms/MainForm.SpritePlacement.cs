using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm
{
    private sealed class StudioSpriteOwner(MainForm studio, StudioDockDocument tab, SpriteEditorControl editor) : ISceneSpriteOwner
    {
        public AssetLocation? Location => editor.Document.Location;
        public object Session => editor.Document;
        public long Revision => editor.Document.Revision;
        public IReadOnlyList<SpriteDefinition> Definitions => editor.Document.Definitions;
        public EditHistory History => editor.Document.History;
        public bool CanPlace => !tab.IsDisposed && tab.Enabled && !editor.IsDisposed && !tab.Document.ContentStale &&
            (!studio._saves.TryGetValue(tab, out var save) || save.IsCompleted);

        public bool CommitEdits() => CanPlace && (!editor.ContainsFocus || editor.CommitEdits());

        public void Select(string member, Guid id)
        {
            if (editor.Document.FindInstance(member, id) is not { } sprite) return;
            if (editor.Document.Definition.Key != member && !tab.Document.SelectMember(member)) return;
            editor.SelectSprite(sprite);
        }

        public bool Place(SpriteInstanceDrag source, ResourceAddress scene, string layerId, PointF position) =>
            editor.Document.PlaceInstance(source, scene, layerId, position);
    }

    private IReadOnlyList<ISceneSpriteOwner> GetSpriteOwners() => _documents
        .Where(tab => !tab.IsDisposed && tab.Document.Editor is SpriteEditorControl { IsDisposed: false })
        .Select(tab => (ISceneSpriteOwner)new StudioSpriteOwner(this, tab, (SpriteEditorControl)tab.Document.Editor))
        .ToArray();

    private IEnumerable<SceneEditorControl> SceneEditors() => _documents
        .Where(tab => !tab.IsDisposed).Select(tab => tab.Document.Editor).OfType<SceneEditorControl>();

    private void SpriteDocumentChanged(object? sender, EventArgs e)
    {
        foreach (var scene in SceneEditors()) scene.InvalidateSpriteOwners();
    }

    private void SpriteRegistryChanged()
    {
        foreach (var scene in SceneEditors()) scene.InvalidateSpriteOwners(reloadSaved: true);
    }

    private void CancelInactiveSpritePlacements()
    {
        foreach (var scene in SceneEditors())
            if (!ReferenceEquals(scene, ActiveDocument?.Document.Editor)) scene.CancelSpritePlacement();
    }

    private bool CommitActiveHistoryEdits() =>
        ActiveDocument?.Document.Editor is SceneEditorControl { SelectedSpriteHistory: not null } scene
            ? scene.CommitSpriteHistoryEdits() : ActiveDocument?.Document.CommitEdits() == true;
}
