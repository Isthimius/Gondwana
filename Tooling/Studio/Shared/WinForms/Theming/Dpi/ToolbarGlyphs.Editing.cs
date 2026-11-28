using Gondwana.Tooling.Studio.Shared.WinForms.Theming;

namespace Gondwana.Tooling.Studio.Shared.WinForms.Dpi;

public static partial class ToolbarGlyphs
{
    /// <summary>Shared semantic artwork assignments for editing commands.</summary>
    public enum EditingCommand { Add, Remove, Delete, Duplicate, Rename, Replace, Prune, Apply, Cancel }

    /// <summary>Loads the assigned editing artwork for the active theme and device-pixel size.</summary>
    public static Bitmap? CreateEditingBitmap(EditingCommand command, int pixels)
    {
        string fileName = command switch
        {
            EditingCommand.Add => "list-add.png",
            EditingCommand.Remove => "list-remove.png",
            EditingCommand.Delete => "edit-delete.png",
            EditingCommand.Duplicate => "edit-copy.png",
            EditingCommand.Rename => "document-edit.png",
            EditingCommand.Replace => "edit-find-replace.png",
            EditingCommand.Prune => "edit-clear.png",
            EditingCommand.Apply => "dialog-ok-apply.png",
            EditingCommand.Cancel => "dialog-cancel.png",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        return ToolingIconResources.TryCreateOxygenBitmap(fileName, pixels);
    }

    /// <summary>Adds an editing command to a text toolbar, retaining the command label and action.</summary>
    public static ToolStripItem AddEditingButton(ToolStrip strip, string text, EditingCommand command, EventHandler click)
    {
        var item = strip.Items.Add(text, null, click);
        SetEditingGlyph(item, command, strip.ImageScalingSize.Width);
        return item;
    }

    /// <summary>Assigns owned artwork to an existing toolbar item without changing its action.</summary>
    public static void SetEditingGlyph(ToolStripItem item, EditingCommand command, int pixels = 16)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (CreateEditingBitmap(command, pixels) is not { } image)
            return;

        item.Image = image;
        item.Disposed += (_, _) => image.Dispose();
    }

    /// <summary>Assigns owned artwork to a standard button while retaining its text.</summary>
    public static void SetEditingGlyph(Button button, EditingCommand command)
    {
        ArgumentNullException.ThrowIfNull(button);

        if (CreateEditingBitmap(command, ToolbarSizing.Pixels(16, button.DeviceDpi)) is not { } image)
            return;

        button.Image = image;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.Disposed += (_, _) => image.Dispose();
    }
}
