using System.Text;
using System.Xml;
using System.Xml.Linq;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.WinForms;

internal sealed class PersistentDockContent(string persistenceId) : DockContent
{
    internal string PersistenceId { get; } = persistenceId;
    protected override string GetPersistString() => PersistenceId;
}

/// <summary>Uses DockPanelSuite's layout format and resolves only owned, existing panes.</summary>
internal sealed class DockLayoutPersistence : IDisposable
{
    private readonly DockPanel _panel;
    private readonly DockLayoutStore _store;
    private readonly Dictionary<string, (PersistentDockContent Pane, Action Place)> _panes = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 400 };
    private byte[]? _observed;
    private byte[]? _saved;
    private bool _started;
    private bool _disposed;
    private double[] _defaultPortions = [];

    internal DockLayoutPersistence(DockPanel panel, string profile)
    {
        _panel = panel;
        _store = new DockLayoutStore(profile);
        _timer.Tick += (_, _) => Observe();
    }

    internal void Register(PersistentDockContent pane, Action place) =>
        _panes.Add(pane.PersistenceId, (pane, place));

    // Call after every expected pane and its default placement have been registered.
    internal void Start()
    {
        if (_started) return;
        _defaultPortions = [_panel.DockLeftPortion, _panel.DockRightPortion,
            _panel.DockTopPortion, _panel.DockBottomPortion];
        // Prevent DockPanelSuite from reactivating a focused child while reparenting.
        bool enabled = _panel.Enabled;
        _panel.Enabled = false;
        try
        {
            if (_store.Read() is { } bytes)
            {
                try { Restore(bytes); }
                catch (Exception ex)
                {
                    DockLayoutStore.Warn(ex);
                    Detach();
                    PlaceDefaults();
                }
            }
            else PlaceDefaults();
        }
        finally { _panel.Enabled = enabled; }
        _started = true;
        _saved = _observed = Capture();
        _timer.Start();
    }

    private void Restore(byte[] bytes)
    {
        // Disallow DTDs before passing the XML to the library's older XML reader.
        using (var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 }))
            XDocument.Load(reader);
        Detach();
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var stream = new MemoryStream(bytes);
            _panel.LoadFromXml(stream, id =>
                _panes.TryGetValue(id, out var entry) && resolved.Add(id) ? entry.Pane : null);
        }
        finally { _panel.ResumeLayout(true, true); }
        foreach (var (id, entry) in _panes)
            if (!resolved.Contains(id) || entry.Pane.Pane is null)
                entry.Place();
    }

    private void Detach()
    {
        foreach (var content in _panel.Contents.Cast<IDockContent>().ToArray())
        {
            content.DockHandler.DockPanel = null;
            // A failed library load may leave temporary placeholders behind.
            if (!_panes.Values.Any(entry => ReferenceEquals(entry.Pane, content)))
                content.DockHandler.Form.Dispose();
        }
        foreach (var pane in _panel.Panes.ToArray()) pane.Dispose();
    }

    private void PlaceDefaults()
    {
        _panel.DockLeftPortion = _defaultPortions[0];
        _panel.DockRightPortion = _defaultPortions[1];
        _panel.DockTopPortion = _defaultPortions[2];
        _panel.DockBottomPortion = _defaultPortions[3];
        foreach (var entry in _panes.Values) entry.Place();
    }

    internal void Reset()
    {
        // Keep open documents alive. Reset is layout-only, even with unsaved documents.
        bool enabled = _panel.Enabled;
        _panel.Enabled = false;
        try
        {
            var documents = _panel.Contents.Cast<IDockContent>()
                .Where(content => !_panes.Values.Any(entry => ReferenceEquals(entry.Pane, content))).ToArray();
            foreach (var document in documents) document.DockHandler.DockPanel = null;
            Detach();
            PlaceDefaults();
            foreach (var document in documents) document.DockHandler.Show(_panel, DockState.Document);
        }
        finally { _panel.Enabled = enabled; }
        _store.Delete();
        _saved = _observed = Capture();
    }

    private byte[] Capture()
    {
        using var stream = new MemoryStream();
        _panel.SaveAsXml(stream, Encoding.UTF8);
        var xml = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()).TrimStart('\uFEFF'));
        // Keyboard focus is transient and must not make an unchanged sibling a writer.
        xml.Root!.SetAttributeValue("ActivePane", -1);
        xml.Root.SetAttributeValue("ActiveDocumentPane", -1);
        foreach (var comment in xml.Nodes().OfType<XComment>().ToArray()) comment.Remove();
        // Documents are deliberately anonymous: no captions, paths, or runtime state.
        foreach (var content in xml.Root!.Element("Contents")!.Elements("Content"))
            if (!_panes.ContainsKey((string?)content.Attribute("PersistString") ?? ""))
                content.SetAttributeValue("PersistString", "ignored-document");
        return Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
    }

    // Sampling the library's small layout snapshot also catches splitter/tab changes
    // that do not raise DockStateChanged. Only stable changes result in disk writes.
    private void Observe()
    {
        if (_disposed || !_panel.IsHandleCreated) return;
        try
        {
            var current = Capture();
            if (Equal(current, _observed) && !Equal(current, _saved) && _store.Write(current))
                _saved = current;
            _observed = current;
        }
        catch (Exception ex) { DockLayoutStore.Warn(ex); }
    }

    private static bool Equal(byte[] a, byte[]? b) => b is not null && a.AsSpan().SequenceEqual(b);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
        if (!_started || _panel.IsDisposed) return;
        try
        {
            var current = Capture();
            // An unchanged sibling must not overwrite the last editor the user rearranged.
            if (!Equal(current, _saved)) _store.Write(current);
        }
        catch (Exception ex) { DockLayoutStore.Warn(ex); }
    }
}
