using Gondwana.Assets;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Shared.WinForms;

namespace Gondwana.Tooling.Studio.Animations.WinForms;

internal sealed partial class MainForm
{
    private readonly EditorSourceMonitorController _sources = new();
    private void MonitorDocument(AnimationEditorDocument tab)
    {
        var model = tab.Document;
        AssetLocation Owner() => new(AssetTypes.AnimationDefinition, model.FilePath ?? Path.Combine(model.BaseDirectory, "Untitled.gani"));
        tab.Sources = _sources.Register(tab, new()
        {
            Location = () => model.FilePath is null ? null : Owner(),
            ReferenceOwner = Owner,
            Accepted = () => model.SourceVersion,
            Dirty = () => model.IsDirty,
            Commit = tab.CommitEdits,
            Password = tab.Editor.SourcePackages.CachedPassword,
            Invalidate = tab.Editor.SourcePackages.Invalidate,
            Roots = () => [(Owner(), model.SerializePacked())],
            SaveAs = () => SaveDocument(tab, true),
            RefreshPreviews = () => { tab.Editor.ReloadSources(); return tab.Editor.SourcesLoadedSuccessfully; },
            Reload = () =>
            {
                if (model.FilePath is null)
                    return;
                var selected = model.Definition.Key;
                var loaded = AnimationDocument.Open(model.FilePath);
                try
                {
                    var member = loaded.Definitions.FirstOrDefault(d => DefinitionReferenceQuery.MembersEqual(AssetTypes.AnimationDefinition, d.Key, selected));
                    if (member is not null)
                        loaded.SelectDefinition(member);
                    EditorSourceMonitorController.Replace(tab, () =>
                    {
                        var replacement = ShowDocument(loaded);
                        return replacement;
                    }, () => tab.CloseApproved = true);
                }
                catch { loaded.Dispose(); throw; }
            }
        });
        model.Changed += tab.Sources.Changed;
        tab.Disposed += (_, _) => model.Changed -= tab.Sources.Changed;
    }
}
