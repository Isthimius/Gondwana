using System.Runtime.ExceptionServices;
using System.Reflection;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Logging;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Audio.GSND;
using Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;
using Microsoft.Extensions.Logging;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

[CollectionDefinition("Project diagnostics global state", DisableParallelization = true)]
public sealed class ProjectDiagnosticsGlobalStateCollection
{
}

[Collection("Project diagnostics global state")]
public sealed class ProjectDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GondwanaDiagnostics-" + Guid.NewGuid().ToString("N"));
    public ProjectDiagnosticsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);
    private string FilePath(string name) => Path.Combine(_root, name);

    [Fact]
    public void DiscoversAllFormatsAndIgnoresBuildDirectories()
    {
        foreach (var extension in new[] { "gts", "GANI", "gsnd", "gscn", "gspr" })
            File.WriteAllText(FilePath("definition." + extension), "{}");
        using (var assets = AssetsFile.LoadOrCreate(FilePath("assets.gaf"), null, false, register: false)) assets.Save();
        foreach (var directory in new[] { "bin", "OBJ", ".git", "nested/bin" })
        {
            Directory.CreateDirectory(FilePath(directory));
            File.WriteAllText(FilePath(directory + "/ignored.gts"), "bad");
        }
        File.WriteAllText(FilePath("ignored.txt"), "bad");
        var result = new ProjectDiagnosticsScanner().Scan(_root);
        Assert.Equal(6, result.Definitions.Count);
        Assert.All(result.Definitions, definition => Assert.True(definition.Loaded));
        Assert.DoesNotContain(result.Problems, problem => problem.Property == "Load");
    }

    [Fact]
    public void ReportsMalformedFilesAndContinuesScanning()
    {
        File.WriteAllText(FilePath("bad.gts"), "not json");
        File.WriteAllText(FilePath("bad.gaf"), "not an archive");
        File.WriteAllText(FilePath("good.gspr"), "{}");
        var result = new ProjectDiagnosticsScanner().Scan(_root);
        Assert.Equal(2, result.Definitions.Count(d => !d.Loaded));
        Assert.Contains(result.Problems, p => p.SourcePath == "bad.gts" && p.Property == "Load");
        Assert.Contains(result.Definitions, d => d.RelativePath == "good.gspr" && d.Loaded);
    }

    [Fact]
    public void ResolvesActualDefinitionRelationshipsRelativeToContainingFile()
    {
        Directory.CreateDirectory(FilePath("nested"));
        TilesheetDefinitionSerializer.Save(FilePath("tiles.gts"), new TilesheetDefinition { Name = "tiles" });
        AnimationDefinitionSerializer.Save(FilePath("nested/walk.gani"), new AnimationDefinition
        {
            Key = "walk",
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("tiles", "../tiles.gts")]
        });
        SceneDefinitionSerializer.Save(FilePath("scene.gscn"), new SceneDefinition
        {
            TilesheetSources = [SceneTilesheetSourceDefinition.Loose("tiles", "tiles.gts")],
            AnimationSources = [SceneAnimationSourceDefinition.Loose("walk", "nested/walk.gani")]
        });
        SpriteDefinitionSerializer.Save(FilePath("player.gspr"), new SpriteDefinition
        {
            SceneSources = [SpriteSceneSourceDefinition.Loose("level", "scene.gscn")],
            TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("tiles", "tiles.gts")]
        });
        var result = new ProjectDiagnosticsScanner().Scan(_root);
        var references = result.Definitions.SelectMany(d => d.References).ToArray();
        Assert.Equal(5, references.Length);
        Assert.All(references, reference => Assert.Null(reference.Problem));
        Assert.Contains(references, reference => reference.Value == "../tiles.gts" && reference.ResolvedPath == FilePath("tiles.gts"));
    }

    [Fact]
    public void ReportsMissingMalformedAndUnsupportedReferences()
    {
        AnimationDefinitionSerializer.Save(FilePath("walk.gani"), new AnimationDefinition
        {
            Key = "walk",
            TilesheetSources =
            [
                AnimationTilesheetSourceDefinition.Loose("missing", "missing.gts"),
                AnimationTilesheetSourceDefinition.Loose("malformed", "bad\0path"),
                new() { Tilesheet = "unknown", Kind = (AnimationTilesheetSourceKind)123 }
            ]
        });
        var result = new ProjectDiagnosticsScanner().Scan(_root);
        Assert.True(Assert.Single(result.Definitions).Loaded);
        Assert.Equal(3, result.Definitions[0].References.Count);
        Assert.All(result.Definitions[0].References, reference => Assert.NotNull(reference.Problem));
        Assert.Contains(result.Problems, p => p.Property == "TilesheetSources[0].GtsPath" && p.Reason.Contains("not found"));
        Assert.Contains(result.Problems, p => p.Property == "TilesheetSources[2].Kind" && p.Reason.Contains("Unsupported"));
    }

    [Fact]
    public void ChecksPackedEntriesAndMediaWithoutRegisteringAssets()
    {
        using (var assets = AssetsFile.LoadOrCreate(FilePath("assets.gaf"), null, false, register: false))
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));
            assets.Add(AssetTypes.TilesheetDefinition, "tiles", stream);
            assets.Save();
        }
        AnimationDefinitionSerializer.Save(FilePath("walk.gani"), new AnimationDefinition
        {
            TilesheetSources =
            [
                AnimationTilesheetSourceDefinition.Packed("tiles", "assets.gaf", "tiles"),
                AnimationTilesheetSourceDefinition.Packed("missing", "assets.gaf", "missing")
            ]
        });
        AudioDefinitionSerializer.Save(FilePath("sound.gsnd"), new AudioDefinition
        {
            Resources = [new() { Key = "sound", FilePath = "missing.wav" },
                new() { Key = "remote", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "https://example.invalid/sound.ogg" },
                new() { Key = "relative-uri", SourceKind = AudioResourceSourceKind.Uri, SourceUri = "audio/sound.ogg" }]
        });
        var result = new ProjectDiagnosticsScanner().Scan(_root);
        var animation = result.Definitions.Single(d => d.Format == "GANI");
        Assert.Null(animation.References[0].Problem);
        Assert.Contains("entry not found", animation.References[1].Problem);
        Assert.DoesNotContain(AssetsFile.AllAssetsFiles, a => a.FilePath == FilePath("assets.gaf"));
        var audio = result.Definitions.Single(d => d.Format == "GSND");
        Assert.Contains("not found", audio.References[0].Problem);
        Assert.Null(audio.References[1].Problem);
        Assert.Null(audio.References[2].Problem);
        Assert.Single(result.Problems, p => p.SourcePath == "sound.gsnd");
    }

    [Fact]
    public void ReusesLoadedPackedArchiveAcrossReferencesWithinScan()
    {
        using (var assets = AssetsFile.LoadOrCreate(FilePath("assets.gaf"), null, false, register: false))
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));
            assets.Add(AssetTypes.TilesheetDefinition, "tiles-1", stream);
            assets.Add(AssetTypes.TilesheetDefinition, "tiles-2", () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}")));
            assets.Save();
        }
        AnimationDefinitionSerializer.Save(FilePath("walk.gani"), new AnimationDefinition
        {
            TilesheetSources =
            [
                AnimationTilesheetSourceDefinition.Packed("tiles-1", "assets.gaf", "tiles-1"),
                AnimationTilesheetSourceDefinition.Packed("tiles-2", "assets.gaf", "tiles-2"),
                AnimationTilesheetSourceDefinition.Packed("missing", "assets.gaf", "missing")
            ]
        });

        using var logging = new EngineLoggerCaptureScope();

        var result = new ProjectDiagnosticsScanner().Scan(_root);

        Assert.Equal(1, logging.Messages.Count(message => message == "Loading assets file."));
        Assert.Contains(result.Problems, p => p.Property == "TilesheetSources[2].AssetsFilePath");
    }

    [Fact]
    public void RescanReplacesResultsAndSupportsCancellation()
    {
        File.WriteAllText(FilePath("first.gspr"), "{}");
        var scanner = new ProjectDiagnosticsScanner();
        var first = scanner.Scan(_root);
        File.Delete(FilePath("first.gspr"));
        File.WriteAllText(FilePath("second.gspr"), "{}");
        Assert.Equal("second.gspr", Assert.Single(scanner.Scan(_root).Definitions).RelativePath);
        Assert.Equal("first.gspr", Assert.Single(first.Definitions).RelativePath);
        Assert.Throws<OperationCanceledException>(() => scanner.Scan(_root, new CancellationToken(true)));
    }

    [Fact]
    public void StudioDiscoversDeployedPluginAndHostsItsDockAndMenu() => RunSta(() =>
    {
        File.WriteAllText(FilePath("sample.gspr"), "{}");
        using var studio = new Gondwana.Tooling.Studio.WinForms.MainForm();
        studio.SetWorkingDirectory(_root);
        var tool = Assert.Single(studio.Workspace.Contents.OfType<WeifenLuo.WinFormsUI.Docking.DockContent>(),
            content => content.Text == "Project Diagnostics");
        var menu = (ToolStripMenuItem)studio.MainMenuStrip!.Items["PluginsMenu"]!;
        var pluginMenu = Assert.Single(menu.DropDownItems.OfType<ToolStripMenuItem>(), item => item.Text == "Project Diagnostics");
        Assert.Equal("Rescan", pluginMenu.DropDownItems[0].Text);
        var tree = Descendants(tool).OfType<TreeView>().Single();
        PumpUntil(() => tree.Nodes.Count == 1);
        Assert.Contains("sample.gspr", tree.Nodes[0].Text);
        Assert.Same(studio.Workspace, tool.DockPanel);
    });

    [Fact]
    public void PluginPanelMenuAndLifecycleReplaceAndClearResults() => RunSta(() =>
    {
        File.WriteAllText(FilePath("first.gspr"), "{}");
        var plugin = new ProjectDiagnosticsPlugin();
        Assert.IsAssignableFrom<Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin>(plugin);
        Assert.Equal("Project Diagnostics", plugin.Name);
        using var panel = plugin.CreatePanel();
        using var menu = plugin.CreateMenuItem();
        var tree = Descendants(panel).OfType<TreeView>().Single();
        plugin.OnProjectOpened(_root);
        PumpUntil(() => tree.Nodes.Count == 1);
        Assert.Contains("first.gspr", tree.Nodes[0].Text);
        File.Delete(FilePath("first.gspr"));
        File.WriteAllText(FilePath("second.gspr"), "{}");
        ((ToolStripMenuItem)menu.DropDownItems[0]).PerformClick();
        Assert.Empty(tree.Nodes.Cast<TreeNode>());
        PumpUntil(() => tree.Nodes.Count == 1);
        Assert.Contains("second.gspr", tree.Nodes[0].Text);
        plugin.OnProjectClosed();
        Assert.Empty(tree.Nodes.Cast<TreeNode>());
        var other = Directory.CreateDirectory(FilePath("other")).FullName;
        plugin.OnProjectOpened(_root);
        plugin.OnProjectOpened(other);
        PumpUntil(() => Descendants(panel).OfType<Label>().Any(l => l.Text.StartsWith("0 definitions")));
        Assert.Empty(tree.Nodes.Cast<TreeNode>());
        plugin.OnProjectOpened(_root);
        panel.Dispose(); // Closing while a scan is active must not marshal to a dead control.
        Application.DoEvents();
    });

    private static IEnumerable<Control> Descendants(Control root) =>
        root.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private static void PumpUntil(Func<bool> done)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Assert.True(done(), "UI did not receive scan results before timeout.");
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class EngineLoggerCaptureScope : IDisposable
    {
        private static readonly FieldInfo LoggerFactoryField =
            typeof(EngineLogger).GetField("_loggerFactory", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo ExternalFactoryField =
            typeof(EngineLogger).GetField("_usingExternalLoggerFactory", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo LoggerCacheField =
            typeof(EngineLogger).GetField("_loggerCache", BindingFlags.Static | BindingFlags.NonPublic)!;

        private readonly object? _originalFactory = LoggerFactoryField.GetValue(null);
        private readonly bool _originalExternalFactory = (bool)ExternalFactoryField.GetValue(null)!;
        private readonly EngineLoggingMode _originalMode = EngineLogger.Mode;

        public List<string> Messages { get; } = [];

        public EngineLoggerCaptureScope()
        {
            EngineLogger.SwitchToSyncAndFlush();
            LoggerFactoryField.SetValue(null, new CapturingLoggerFactory(Messages));
            ExternalFactoryField.SetValue(null, true);
            LoggerCacheField.GetValue(null)!.GetType()
                .GetMethod(nameof(System.Collections.IDictionary.Clear))!
                .Invoke(LoggerCacheField.GetValue(null), null);
        }

        public void Dispose()
        {
            LoggerFactoryField.SetValue(null, _originalFactory);
            ExternalFactoryField.SetValue(null, _originalExternalFactory);
            LoggerCacheField.GetValue(null)!.GetType()
                .GetMethod(nameof(System.Collections.IDictionary.Clear))!
                .Invoke(LoggerCacheField.GetValue(null), null);
            EngineLogger.Mode = _originalMode;
        }
    }

    private sealed class CapturingLoggerFactory(List<string> messages) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NoOpScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        public static NoOpScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
