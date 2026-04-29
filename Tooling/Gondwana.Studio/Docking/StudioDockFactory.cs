using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.Docking;

/// <summary>
/// Builds the initial docking layout:
///   RootDock
///   └── ProportionalDock (Horizontal)
///       ├── ToolDock  (left – Directory panel)
///       ├── ProportionalDockSplitter
///       └── DocumentDock (right – document tabs)
/// </summary>
public sealed class StudioDockFactory : Factory
{
    private readonly DirectoryPanelViewModel _directoryVm;
    private DocumentDock? _documentDock;

    public StudioDockFactory(DirectoryPanelViewModel directoryVm)
    {
        _directoryVm = directoryVm;
    }

    public override IRootDock CreateLayout()
    {
        // ---- Directory tool ------------------------------------------------
        var directoryTool = new Tool
        {
            Id = "Directory",
            Title = "Directory",
            Context = _directoryVm
        };

        var leftToolDock = new ToolDock
        {
            Id = "LeftTools",
            Title = "Tools",
            Proportion = 0.22,
            VisibleDockables = CreateList<IDockable>(directoryTool),
            ActiveDockable = directoryTool,
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible
        };

        // ---- Document area -------------------------------------------------
        _documentDock = new DocumentDock
        {
            Id = "Documents",
            Title = "Documents",
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>(),
            CanCreateDocument = false
        };

        // ---- Main horizontal split -----------------------------------------
        var mainLayout = new ProportionalDock
        {
            Id = "Main",
            Title = "Main",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftToolDock,
                new ProportionalDockSplitter { Id = "Splitter" },
                _documentDock
            )
        };

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "Root";
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Directory"] = () => _directoryVm
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>();

        base.InitLayout(layout);
    }

    /// <summary>Opens a new document tab in the document dock area.</summary>
    public Document? OpenDocument(string id, string title, object context)
    {
        if (_documentDock is null) return null;

        // Reuse existing tab with the same id
        if (_documentDock.VisibleDockables is not null)
        {
            foreach (var dockable in _documentDock.VisibleDockables)
            {
                if (dockable.Id == id)
                {
                    SetActiveDockable(dockable);
                    SetFocusedDockable(_documentDock, dockable);
                    return dockable as Document;
                }
            }
        }

        var document = new Document
        {
            Id = id,
            Title = title,
            Context = context
        };

        AddDockable(_documentDock, document);
        SetActiveDockable(document);
        SetFocusedDockable(_documentDock, document);

        return document;
    }
}
