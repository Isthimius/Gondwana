using System.Collections.Specialized;
using System.Drawing;
using Gondwana.Tooling.Studio.ViewModels;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>Visual severity assigned to an Output line; the underlying log text remains unchanged.</summary>
internal enum OutputSeverity { Normal, Important, Warning, Error }

/// <summary>
/// Panel that shows log output from the studio.
/// MainForm hosts this control in a global tool DockContent.
/// </summary>
public sealed class OutputPanel : UserControl
{
    private readonly OutputViewModel _vm;
    private readonly RichTextBox _textBox;

    /// <summary>Displays existing output lines and subscribes to subsequent collection changes.</summary>
    /// <param name="vm">The borrowed output model; the panel does not clear or dispose it.</param>
    public OutputPanel(OutputViewModel vm)
    {
        _vm = vm;

        _textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = ToolingAppearance.Palette.Background,
            ForeColor = ToolingAppearance.Palette.Foreground,
            Font = new Font("Consolas", 9f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = false
        };
        Controls.Add(_textBox);

        _vm.Lines.CollectionChanged += OnLinesChanged;

        foreach (var line in _vm.Lines)
            AppendLine(line);
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        if (_textBox.InvokeRequired)
        {
            _textBox.BeginInvoke(() => OnLinesChanged(sender, e));
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null &&
            e.NewStartingIndex == _vm.Lines.Count - e.NewItems.Count)
        {
            foreach (string line in e.NewItems)
                AppendLine(line);
        }
        else
        {
            // Non-append edits need a full redraw so text and colors stay in the same order.
            _textBox.Clear();
            foreach (var line in _vm.Lines)
                AppendLine(line);
        }
    }

    private void AppendLine(string line)
    {
        _textBox.Select(_textBox.TextLength, 0);
        _textBox.SelectionColor = ColorFor(Classify(line), ToolingAppearance.Current);
        _textBox.SelectedText = line + Environment.NewLine;
        _textBox.SelectionColor = _textBox.ForeColor; // Do not leak this color into the next line.
        _textBox.ScrollToCaret();
    }

    /// <summary>
    /// Recognizes explicit severity markers after the standard HH:mm:ss timestamp.
    /// Unmarked text stays neutral; incidental words such as a filename containing "error" do not imply severity.
    /// </summary>
    internal static OutputSeverity Classify(string line)
    {
        ReadOnlySpan<char> message = line.AsSpan().TrimStart();
        if (message.Length > 9 && char.IsDigit(message[0]) && char.IsDigit(message[1]) &&
            message[2] == ':' && char.IsDigit(message[3]) && char.IsDigit(message[4]) &&
            message[5] == ':' && char.IsDigit(message[6]) && char.IsDigit(message[7]) &&
            message[8] == ' ')
            message = message[9..].TrimStart();

        if (Marked(message, "Error") || Marked(message, "Critical") || Marked(message, "Fatal"))
            return OutputSeverity.Error;
        if (Marked(message, "Warning") || Marked(message, "Warn"))
            return OutputSeverity.Warning;
        if (Marked(message, "Important") || Marked(message, "Info"))
            return OutputSeverity.Important;
        return OutputSeverity.Normal;
    }

    private static bool Marked(ReadOnlySpan<char> text, string level) =>
        text.StartsWith(level + ":", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("[" + level + "]", StringComparison.OrdinalIgnoreCase);

    /// <summary>Dark and light palettes use different shades to maintain contrast with their backgrounds.</summary>
    internal static Color ColorFor(OutputSeverity severity, ToolingTheme theme) => (severity, theme) switch
    {
        (OutputSeverity.Warning, ToolingTheme.Light) => Color.FromArgb(143, 95, 0),
        (OutputSeverity.Warning, _) => Color.FromArgb(255, 206, 99),
        (OutputSeverity.Error, ToolingTheme.Light) => Color.FromArgb(178, 34, 34),
        (OutputSeverity.Error, _) => Color.FromArgb(255, 111, 112),
        (OutputSeverity.Important, ToolingTheme.Light) => Color.FromArgb(0, 86, 173),
        (OutputSeverity.Important, _) => Color.FromArgb(114, 186, 255),
        _ => ToolingPalette.For(theme).Foreground
    };

    /// <summary>Detaches from the output model and releases the panel's controls.</summary>
    /// <param name="disposing">Whether to detach managed event subscriptions and release managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _vm.Lines.CollectionChanged -= OnLinesChanged;

        base.Dispose(disposing);
    }
}
