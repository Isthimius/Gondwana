using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Shared.WinForms;
using Gondwana.Tooling.Audio.Editing;

namespace Gondwana.Tooling.Audio.WinForms;

internal sealed partial class MainForm
{
    private readonly EditorSourceMonitorController _sources = new();
    private void MonitorDocument(AudioEditorDocument tab)
    {
        var model = tab.Document;
        AssetLocation Owner() => new(AssetTypes.AudioDefinition, model.FilePath ?? Path.Combine(model.BaseDirectory, "Untitled.gsnd"));
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
            RefreshPreviews = () => tab.Editor.UpdateValidation(),
            Reload = () =>
            {
                if (model.FilePath is null) return;
                var selected = model.Definition.Key;
                var loaded = AudioDocument.Open(model.FilePath);
                try
                {
                    var member = loaded.Definitions.FirstOrDefault(d => string.Equals(d.Key, selected, StringComparison.OrdinalIgnoreCase));
                    if (member is not null) loaded.SelectDefinition(member);
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
