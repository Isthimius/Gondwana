namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Provides data for the event that is raised when a <see cref="Tilesheet"/> is disposed.
/// </summary>
public class TilesheetDisposedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the <see cref="Tilesheet"/> that was disposed.
    /// </summary>
    public Tilesheet Tilesheet;

    /// <summary>
    /// Initializes a new instance of the <see cref="TilesheetDisposedEventArgs"/> class.
    /// </summary>
    /// <param name="tilesheet">The tilesheet that was disposed.</param>
    protected internal TilesheetDisposedEventArgs(Tilesheet tilesheet)
    {
        Tilesheet = tilesheet;
    }
}