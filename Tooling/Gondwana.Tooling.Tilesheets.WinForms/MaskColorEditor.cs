using System.ComponentModel;
using System.Drawing.Design;
using MaskRgb = Gondwana.Tooling.Tilesheets.WinForms.PropertyFields.MaskRgb;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>Edits the RGB components of a GTS mask with a single modal color picker.</summary>
internal sealed class MaskColorEditor : UITypeEditor
{
    /// <inheritdoc/>
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.Modal;

    /// <inheritdoc/>
    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
    {
        using var dialog = new ColorDialog
        {
            Color = value is MaskRgb current ? Color.FromArgb(current.Red, current.Green, current.Blue) : Color.Black,
            FullOpen = true
        };

        return dialog.ShowDialog() == DialogResult.OK
            ? new MaskRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B)
            : value;
    }

    /// <inheritdoc/>
    public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc/>
    public override void PaintValue(PaintValueEventArgs e)
    {
        if (e.Value is not MaskRgb color)
            return;

        using var brush = new SolidBrush(Color.FromArgb(color.Red, color.Green, color.Blue));

        e.Graphics.FillRectangle(brush, e.Bounds);
        e.Graphics.DrawRectangle(Pens.Black, e.Bounds.Left, e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - 1), Math.Max(0, e.Bounds.Height - 1));
    }
}
