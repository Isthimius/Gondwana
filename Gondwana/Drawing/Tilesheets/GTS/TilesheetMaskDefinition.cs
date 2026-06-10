namespace Gondwana.Drawing.Tilesheets.GTS;

public sealed class TilesheetMaskDefinition
{
    public byte Red { get; set; }

    public byte Green { get; set; }

    public byte Blue { get; set; }

    public byte Alpha { get; set; } = 255;

    public byte Tolerance { get; set; } = 5;
}