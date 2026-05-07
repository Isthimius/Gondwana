using System.Collections.ObjectModel;

namespace Gondwana.Studio.ViewModels;

public sealed class OutputViewModel : ViewModelBase
{
    public ObservableCollection<string> Lines { get; } = [];

    public string CombinedText => string.Join(Environment.NewLine, Lines);

    public void Log(string message)
    {
        Lines.Add($"{DateTimeOffset.Now:HH:mm:ss} {message}");
        OnPropertyChanged(nameof(CombinedText));
    }
}
