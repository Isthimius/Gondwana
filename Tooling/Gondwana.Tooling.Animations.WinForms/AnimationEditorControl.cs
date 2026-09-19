using Gondwana.Drawing.Animation.GANI;

namespace Gondwana.Tooling.Animations.WinForms;

/// <summary>
/// Hostable WinForms surface for editing one GANI animation definition.
/// The standalone application hosts this control inside a dock document; Studio can
/// host the same control later without depending on MainForm.
/// </summary>
public sealed class AnimationEditorControl : UserControl
{
    private readonly Label _placeholder = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Text = "GANI editor"
    };

    /// <summary>
    /// Gets the definition currently owned by this editor surface.
    /// </summary>
    public AnimationDefinition Definition { get; private set; }

    public AnimationEditorControl()
        : this(new AnimationDefinition())
    {
    }

    public AnimationEditorControl(AnimationDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Dock = DockStyle.Fill;
        Controls.Add(_placeholder);
        DarkTheme.Apply(this);
    }

    /// <summary>
    /// Replaces the definition being edited.
    /// </summary>
    public void LoadDefinition(AnimationDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
}
