using System.Collections.Specialized;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

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
            BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
            Font = new System.Drawing.Font("Consolas", 9f),
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

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (string line in e.NewItems)
                AppendLine(line);
        }
        else
        {
            _textBox.Clear();
            foreach (var line in _vm.Lines)
                AppendLine(line);
        }
    }

    private void AppendLine(string line)
    {
        _textBox.AppendText(line + Environment.NewLine);
        _textBox.ScrollToCaret();
    }

    /// <summary>Detaches from the output model and releases the panel's controls.</summary>
    /// <param name="disposing">Whether to detach managed event subscriptions and release managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _vm.Lines.CollectionChanged -= OnLinesChanged;

        base.Dispose(disposing);
    }
}
