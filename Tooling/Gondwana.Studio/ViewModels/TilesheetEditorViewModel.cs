using System.IO;
using Avalonia.Media.Imaging;
using Gondwana.Studio.Core.Services;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Avalonia-specific tilesheet editor ViewModel.
/// Extends the framework-agnostic base with an Avalonia <see cref="Bitmap"/> preview property
/// so that the view can display the tilesheet image.
/// </summary>
public sealed partial class TilesheetEditorViewModel : TilesheetEditorViewModelBase
{
    private Bitmap? _previewBitmap;

    /// <summary>
    /// Gets or sets the Avalonia bitmap used to render the tilesheet preview in the view.
    /// </summary>
    public Bitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set
        {
            var previousBitmap = _previewBitmap;
            if (SetProperty(ref _previewBitmap, value))
            {
                previousBitmap?.Dispose();
            }
        }
    }

    /// <summary>
    /// TilesheetEditorViewModel.
    /// </summary>
    /// <param name="dialogService">Platform dialog service.</param>
    public TilesheetEditorViewModel(IDialogService dialogService)
        : base(dialogService)
    {
    }

    /// <inheritdoc/>
    public override void LoadImage(string path)
    {
        // Let the base class read dimensions via SkiaSharp and rebuild the tile grid.
        base.LoadImage(path);

        // Then load the Avalonia bitmap for visual preview.
        try
        {
            using var stream = File.OpenRead(path);
            PreviewBitmap = new Bitmap(stream);
        }
        catch
        {
            PreviewBitmap = null;
        }
    }
}
