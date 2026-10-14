namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>A physical GTS document containing independently identified logical definitions.</summary>
public sealed class TilesheetDefinitionDocument
{
    /// <summary>Gets or sets the definitions in document order.</summary>
    public List<TilesheetDefinition> Definitions { get; set; } = [];
}
