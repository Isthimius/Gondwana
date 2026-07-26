namespace Gondwana.Widgets.Dialogs;

/// <summary>
/// Represents the result produced when a dialog closes.
/// </summary>
public enum DialogResult
{
    /// <summary>
    /// No result has been selected.
    /// </summary>
    None = 0,

    /// <summary>
    /// The dialog was accepted.
    /// </summary>
    OK = 1,

    /// <summary>
    /// The dialog was cancelled.
    /// </summary>
    Cancel = 2,

    /// <summary>
    /// The user selected Yes.
    /// </summary>
    Yes = 3,

    /// <summary>
    /// The user selected No.
    /// </summary>
    No = 4,

    /// <summary>
    /// The dialog was closed without a more specific result.
    /// </summary>
    Close = 5
}
