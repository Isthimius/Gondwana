using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Shared.WinForms;
using Gondwana.Tooling.Studio.Tilesheets.WinForms.Editing;

namespace Gondwana.Tooling.Studio.Tilesheets.WinForms;

internal sealed partial class MainForm
{
    private readonly EditorSourceMonitorController _sources = new();
    private void MonitorDocument(EditorDocument tab)
    {
        var model = tab.Document;
        AssetLocation Owner() => new(AssetTypes.TilesheetDefinition, model.FilePath ?? Path.Combine(model.BaseDirectory, "Untitled.gts"));
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
            RefreshPreviews = () =>
            {
                tab.Editor.ReloadImage();
                return tab.Editor.PreviewLoadSucceeded;
            },
            Reload = () =>
            {
                if (model.FilePath is null)
                    return;
                var selected = model.Definition.Name;
                var zoom = tab.Editor.PreviewZoom;
                var loaded = TilesheetDocument.Open(model.FilePath);
                try
                {
                    var member = loaded.Definitions.FirstOrDefault(d => DefinitionReferenceQuery.MembersEqual(AssetTypes.TilesheetDefinition, d.Name, selected));
                    if (member is not null)
                        loaded.SelectDefinition(member);
                    EditorSourceMonitorController.Replace(tab, () =>
                    {
                        var replacement = ShowDocument(loaded);
                        replacement.Editor.PreviewZoom = zoom;
                        return replacement;
                    }, () => tab.CloseApproved = true);
                }
                catch
                {
                    loaded.Dispose();
                    throw;
                }
            }
        });
        model.Changed += tab.Sources.Changed;
        tab.Disposed += (_, _) => model.Changed -= tab.Sources.Changed;
    }
}
