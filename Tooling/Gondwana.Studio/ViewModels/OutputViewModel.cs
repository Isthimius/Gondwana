using System.Collections.ObjectModel;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// OutputViewModel.
/// </summary>
public sealed class OutputViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the output lines.
    /// </summary>
    public ObservableCollection<string> Lines { get; } = [];

    /// <summary>
    /// Gets all output lines joined with newlines.
    /// </summary>
    public string CombinedText => string.Join(Environment.NewLine, Lines);

    /// <summary>
    /// Log.
    /// </summary>
    /// <param name="message">message.</param>
    public void Log(string message)
    {
        Lines.Add($"{DateTimeOffset.Now:HH:mm:ss} {message}");
        OnPropertyChanged(nameof(CombinedText));
    }
}
