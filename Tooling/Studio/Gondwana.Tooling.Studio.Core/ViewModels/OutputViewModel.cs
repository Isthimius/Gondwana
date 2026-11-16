using System.Collections.ObjectModel;

namespace Gondwana.Tooling.Studio.ViewModels;

/// <summary>Stores Studio output lines and exposes their combined text for binding.</summary>
public sealed class OutputViewModel : ViewModelBase
{
    /// <summary>Gets the mutable, ordered output collection used by the Output pane.</summary>
    /// <remarks>Direct edits notify collection listeners; only <see cref="Log"/> raises a property-change notification for <see cref="CombinedText"/>.</remarks>
    public ObservableCollection<string> Lines { get; } = [];

    /// <summary>
    /// Gets all output lines joined with newlines.
    /// </summary>
    public string CombinedText => string.Join(Environment.NewLine, Lines);

    /// <summary>Appends a line prefixed with the local time in HH:mm:ss format and notifies combined-text bindings.</summary>
    /// <param name="message">The text to append after the timestamp.</param>
    /// <remarks>Updates and notifications run synchronously on the calling thread; no dispatcher or locking is provided.</remarks>
    public void Log(string message)
    {
        Lines.Add($"{DateTimeOffset.Now:HH:mm:ss} {message}");
        OnPropertyChanged(nameof(CombinedText));
    }
}
