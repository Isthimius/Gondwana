using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm
{
    internal static ResourceAddress? DirectoryDragAddress(TreeNode node)
    {
        if (node.Tag is PackedAssetNodeIdentity packed)
            return new(new AssetLocation(packed.AssetType, packed.ArchivePath, packed.EntryName));

        if (node.Tag is not string path)
            return null;

        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".gts" => AssetTypes.TilesheetDefinition,
            ".gani" => AssetTypes.AnimationDefinition,
            ".gscn" => AssetTypes.SceneDefinition,
            _ => AssetTypes.Misc
        };
        return type == AssetTypes.Misc ? null : new(new AssetLocation(type, path));
    }

    private void StartResourceDrag(Control source, ResourceAddress address)
    {
        if (CreateResourceDrag(address) is { } data)
            source.DoDragDrop(data, DragDropEffects.Copy);
    }

    internal ResourceDragData? CreateResourceDrag(ResourceAddress address) => address.Location.AssetType switch
    {
        AssetTypes.TilesheetDefinition => new ResourceDragData<TilesheetFrameDrag>(address, (owner, resolver) =>
            PickFrame(owner, address, resolver)),
        AssetTypes.AnimationDefinition when address.Member is not null => new ResourceDragData<AnimationDrag>(new(address)),
        AssetTypes.AnimationDefinition => new ResourceDragData<AnimationDrag>(address, (owner, resolver) =>
            PickMember(owner, address, resolver) is { } selected ? new(selected) : null),
        AssetTypes.SceneDefinition => new ResourceDragData<SceneLayerDrag>(address, (owner, resolver) =>
            PickLayer(owner, address, resolver)),
        _ => null
    };

    internal Func<IWin32Window, string, IReadOnlyList<string>, string?> ChooseDragItem { get; set; } = ChooseItem;
    internal Func<IWin32Window, long, long, Point?> ChooseDragCoordinates { get; set; } = ChooseCoordinates;

    private ResourceAddress? PickMember(IWin32Window owner, ResourceAddress address, AuthoringResourceResolver resolver)
    {
        if (address.Member is not null)
            return address;

        var members = resolver.GetMembers(address.Resource);
        if (members.Count == 0)
            throw new InvalidDataException("The dragged resource contains no saved definitions.");

        var member = members.Count == 1 ? members[0] : ChooseDragItem(owner, "Choose a definition to assign", members);
        return member is null ? null : address with { Member = member };
    }

    private TilesheetFrameDrag? PickFrame(IWin32Window owner, ResourceAddress address, AuthoringResourceResolver resolver)
    {
        if (PickMember(owner, address, resolver) is not { } selected)
            return null;

        var sheet = resolver.LoadValidatedTilesheet(selected);
        var regions = sheet.Regions.Select(region => region.Name).ToArray();
        if (regions.Length == 0)
            throw new InvalidDataException("The tilesheet has no frame regions.");

        string? regionName = regions.Length == 1 ? regions[0] : ChooseDragItem(owner, "Choose a frame region", regions);
        if (regionName is null)
            return null;

        var region = sheet.Regions.Single(item => item.Name == regionName);
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        return ChooseDragCoordinates(owner, columns, rows) is { } frame
            ? new(selected, region.Name, frame.X, frame.Y) : null;
    }

    private SceneLayerDrag? PickLayer(IWin32Window owner, ResourceAddress address, AuthoringResourceResolver resolver)
    {
        if (PickMember(owner, address, resolver) is not { } selected)
            return null;

        var scene = resolver.LoadScene(selected.Resource, selected.Member);
        var layers = scene.Layers.Select(layer => layer.ID).ToArray();
        if (layers.Length == 0)
            throw new InvalidDataException("The scene has no layers to assign.");

        return ChooseDragItem(owner, "Choose a scene layer to assign", layers) is { } layer
            ? new(selected, layer) : null;
    }

    private static string? ChooseItem(IWin32Window owner, string title, IReadOnlyList<string> choices)
    {
        using var dialog = CreateDragPicker(title);
        var items = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        items.Items.AddRange(choices.Cast<object>().ToArray());
        var accept = AddPickerButtons(dialog);
        accept.Enabled = false;
        items.SelectedIndexChanged += (_, _) => accept.Enabled = items.SelectedIndex >= 0;
        items.DoubleClick += (_, _) => accept.PerformClick();
        dialog.Controls.Add(items);
        items.BringToFront();
        ToolingAppearance.Apply(dialog);
        return dialog.ShowDialog(owner) == DialogResult.OK ? items.SelectedItem as string : null;
    }

    private static Point? ChooseCoordinates(IWin32Window owner, long columns, long rows)
    {
        if (columns <= 0 || rows <= 0)
            throw new InvalidDataException("The selected region contains no frames.");

        using var dialog = CreateDragPicker("Choose frame coordinates");
        dialog.ClientSize = new Size(420, 110);
        var x = new NumericUpDown { Maximum = Math.Min(columns - 1, int.MaxValue) };
        var y = new NumericUpDown { Maximum = Math.Min(rows - 1, int.MaxValue) };
        var coordinates = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        coordinates.Controls.AddRange([new Label { Text = "Column (X)", AutoSize = true }, x,
            new Label { Text = "Row (Y)", AutoSize = true }, y]);
        AddPickerButtons(dialog);
        dialog.Controls.Add(coordinates);
        coordinates.BringToFront();
        ToolingAppearance.Apply(dialog);
        return dialog.ShowDialog(owner) == DialogResult.OK ? new Point((int)x.Value, (int)y.Value) : null;
    }

    private static Form CreateDragPicker(string title) => new()
    {
        Text = title,
        ClientSize = new Size(440, 300),
        StartPosition = FormStartPosition.CenterParent,
        ShowInTaskbar = false,
        MinimizeBox = false,
        MaximizeBox = false
    };

    private static Button AddPickerButtons(Form dialog)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var accept = new Button { Text = "Assign", DialogResult = DialogResult.OK, AutoSize = true };
        bar.Controls.AddRange([cancel, accept]);
        dialog.Controls.Add(bar);
        dialog.CancelButton = cancel;
        dialog.AcceptButton = accept;
        return accept;
    }
}
