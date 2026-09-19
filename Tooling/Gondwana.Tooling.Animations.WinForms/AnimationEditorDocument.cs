using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class AnimationEditorDocument : DockContent
{
    public AnimationEditorControl Editor { get; } = new();

    public AnimationEditorDocument()
    {
        Text = "Untitled.gani";
        DockAreas = DockAreas.Document | DockAreas.Float;
        Controls.Add(Editor);
        DarkTheme.Apply(this);
    }
}
