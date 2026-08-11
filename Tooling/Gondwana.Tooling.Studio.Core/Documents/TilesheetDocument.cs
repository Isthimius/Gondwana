using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tooling.Studio.Documents;

/// <summary>
/// Represents an editor session for a tilesheet, including editable definition data and source provenance.
/// </summary>
public sealed class TilesheetDocument
{
    private TilesheetDefinition _definition = new();

    /// <summary>
    /// Gets or sets the runtime tilesheet currently loaded for preview and authoring.
    /// This may be <see langword="null"/> before the definition has been materialized into runtime data.
    /// </summary>
    public Tilesheet? Tilesheet { get; set; }

    /// <summary>
    /// Gets or sets the editable tilesheet definition.
    /// </summary>
    public TilesheetDefinition Definition
    {
        get => _definition;
        set => _definition = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets a value indicating whether the document has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// Gets or sets the preferred save target for updating the current definition source.
    /// </summary>
    public string? SaveTargetPath { get; set; }

    /// <summary>
    /// Gets or sets the preferred export target for writing a new definition file.
    /// </summary>
    public string? ExportTargetPath { get; set; }
}
