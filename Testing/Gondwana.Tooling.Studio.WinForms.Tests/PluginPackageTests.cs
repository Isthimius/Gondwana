using System.Runtime.Loader;
using System.Text.Json;
using Gondwana.Tooling.Studio.Core.Extensibility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Host = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

/// <summary>Real assemblies exercise package validation and load-context boundaries without UI automation.</summary>
public sealed class PluginPackageTests
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "plugin-fixtures", Guid.NewGuid().ToString("N"));
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
        .Append(typeof(IStudioPlugin).Assembly.Location).Distinct()
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    [Fact]
    public void PackageLoadsOnceAndSnapshotIsImmutable()
    {
        Package("one", "sample.one");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        var plugin = Assert.Single(host.Plugins);
        Assert.Equal("Fixture", plugin.Name);
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly));
        var before = host.Discovery;
        var row = Assert.Single(before);
        Assert.Equal("sample.one", row.Id);
        Assert.Equal("1.0.0", row.Version);
        Assert.False(row.IsLegacy);
        Assert.Equal(PluginCompatibility.Compatible, row.Compatibility);
        host.DiscoverAndLoad(_root);
        Assert.Same(plugin, Assert.Single(host.Plugins));
        Assert.Single(host.Discovery);
        host.NotifyProjectOpened("throw");
        Assert.Empty(host.Plugins);
        Assert.Equal(PluginLoadStatus.Disabled, Assert.Single(host.Discovery).Status);
        Assert.Equal(PluginLoadStatus.Loaded, Assert.Single(before).Status);
    }

    [Fact]
    public void LegacyMetadataIsUnknownAndPackagedCopyWins()
    {
        var entry = Package("one", "sample.one");
        File.Copy(entry, Path.Combine(_root, Path.GetFileName(entry)));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        var legacy = Assert.Single(host.Discovery, row => row.IsLegacy);
        Assert.StartsWith("legacy:", legacy.Id);
        Assert.Null(legacy.Version);
        Assert.Equal(PluginCompatibility.Unknown, legacy.Compatibility);
        Assert.Equal(PluginLoadStatus.Conflict, legacy.Status);
    }

    [Fact]
    public void LegacyFlatPluginStillLoadsOnce()
    {
        Directory.CreateDirectory(_root);
        Compile(Path.Combine(_root, "Legacy.dll"), PluginSource("Legacy"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        host.DiscoverAndLoad(_root);
        Assert.Equal("Fixture", Assert.Single(host.Plugins).Name);
        Assert.True(Assert.Single(host.Discovery).IsLegacy);
    }

    [Fact]
    public void DuplicateIdsRejectBothBeforeConstructorsRun()
    {
        Package("a", "sample.same", source: PluginSource("First", constructor: "throw new System.Exception(\"must not execute\");"));
        Package("b", "sample.same");
        Package("c", "sample.good");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Equal(2, host.Discovery.Count(row => row.Status == PluginLoadStatus.Conflict));
        Assert.DoesNotContain(host.Discovery, row => row.Status == PluginLoadStatus.FailedToLoad);
    }

    [Fact]
    public void LaterConflictingIdDisablesEarlierInstanceWithoutReloading()
    {
        Package("a", "sample.same");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Package("b", "sample.same");
        host.DiscoverAndLoad(_root);
        Assert.Empty(host.Plugins);
        Assert.All(host.Discovery, row => Assert.Equal(PluginLoadStatus.Conflict, row.Status));
    }

    [Fact]
    public void DistinctIdsWithSameImplementationActivateOnlyOnce()
    {
        var entry = Package("a", "sample.a");
        var second = Path.Combine(_root, "b");
        Directory.CreateDirectory(second);
        File.Copy(entry, Path.Combine(second, Path.GetFileName(entry)));
        Manifest(second, "sample.b", Path.GetFileName(entry));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Single(host.Discovery, row => row.Status == PluginLoadStatus.Conflict);
    }

    [Theory]
    [InlineData("{}", PluginLoadStatus.InvalidPackage)]
    [InlineData("{", PluginLoadStatus.InvalidPackage)]
    [InlineData("null", PluginLoadStatus.InvalidPackage)]
    [InlineData("schema", PluginLoadStatus.InvalidPackage)]
    [InlineData("version", PluginLoadStatus.InvalidPackage)]
    [InlineData("prerelease", PluginLoadStatus.InvalidPackage)]
    [InlineData("contract", PluginLoadStatus.Incompatible)]
    [InlineData("missing-contract", PluginLoadStatus.InvalidPackage)]
    [InlineData("missing-name", PluginLoadStatus.InvalidPackage)]
    [InlineData("missing-id", PluginLoadStatus.InvalidPackage)]
    [InlineData("missing-entry", PluginLoadStatus.InvalidPackage)]
    [InlineData("missing-manifest", PluginLoadStatus.InvalidPackage)]
    public void InvalidPackagesRemainVisibleAndDoNotBlockHealthyPackages(string error, PluginLoadStatus status)
    {
        var entry = Package("bad", "sample.bad");
        var path = Path.Combine(Path.GetDirectoryName(entry)!, "plugin.json");
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path))!;
        switch (error)
        {
            case "schema": data["schemaVersion"] = 2; break;
            case "contract": data["contractVersion"] = 2; break;
            case "version": data["version"] = "1.0"; break;
            case "prerelease": data["version"] = "1.0.0-01"; break;
            case "missing-contract": data.Remove("contractVersion"); break;
            case "missing-name": data.Remove("name"); break;
            case "missing-id": data.Remove("id"); break;
            case "missing-entry": File.Delete(entry); break;
        }
        File.WriteAllText(path, error is "{}" or "{" or "null" ? error : JsonSerializer.Serialize(data));
        if (error == "missing-manifest") File.Delete(path);
        Package("good", "sample.good");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        var row = Assert.Single(host.Discovery, row => row.SourceDirectory.EndsWith("bad", StringComparison.Ordinal));
        Assert.Equal(status, row.Status);
        Assert.False(string.IsNullOrWhiteSpace(row.Diagnostic));
    }

    [Theory]
    [InlineData("../Outside.dll")]
    [InlineData("..\\Outside.dll")]
    [InlineData("/tmp/Outside.dll")]
    [InlineData("C:\\Outside.dll")]
    [InlineData("folder/Entry.dll")]
    [InlineData("Entry.dll:stream")]
    public void InvalidEntrypointPathsAreRejected(string entry)
    {
        var directory = Path.Combine(_root, "bad");
        Directory.CreateDirectory(directory);
        Manifest(directory, "sample.bad", entry);
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal(PluginLoadStatus.InvalidPackage, Assert.Single(host.Discovery).Status);
        Assert.Empty(host.Plugins);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedAssemblyCopiesAreRejectedEvenWhenRenamed(bool renamed)
    {
        var entry = Package("bad", "sample.bad");
        File.Copy(typeof(IStudioPlugin).Assembly.Location,
            Path.Combine(Path.GetDirectoryName(entry)!, renamed ? "Renamed.dll" : "Gondwana.Studio.Core.dll"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal(PluginLoadStatus.InvalidPackage, Assert.Single(host.Discovery).Status);
        Assert.Empty(host.Plugins);
    }

    [Fact]
    public void PrivateDependencyVersionsAreIsolated()
    {
        foreach (var version in new[] { 1, 2 })
        {
            var directory = Path.Combine(_root, "v" + version);
            Directory.CreateDirectory(directory);
            var dependency = Path.Combine(directory, "PrivateHelper.dll");
            Compile(dependency, $"[assembly: System.Reflection.AssemblyVersion(\"{version}.0.0.0\")] public static class Helper {{ public static string Value => \"version{version}\"; }}");
            Package("v" + version, "sample.v" + version, "Plugin" + version,
                PluginSource("Plugin" + version, "Helper.Value"), dependency);
            DependencyManifest(directory, "Plugin" + version, version);
        }
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal(new[] { "version1", "version2" }, host.Plugins.Select(plugin => plugin.Name));
        Assert.All(host.Discovery, row => Assert.Equal(PluginLoadStatus.Loaded, row.Status));
    }

    [Fact]
    public void MissingPrivateDependencyCannotFallBackToDefaultContext()
    {
        var directory = Path.Combine(_root, "missing");
        Directory.CreateDirectory(directory);
        var name = "MissingHelper" + Guid.NewGuid().ToString("N");
        var dependency = Path.Combine(_root, name + ".dll");
        Compile(dependency, "public static class Helper { public static string Value => \"wrong-context\"; }");
        AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency);
        Package("missing", "sample.missing", source: PluginSource("Missing", "Helper.Value"), dependency: dependency);
        // Keep the helper outside plugins/ so it cannot be resolved beside the entry DLL.
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Empty(host.Plugins);
        Assert.Contains(host.Discovery, row => row.Id == "sample.missing" && row.Status == PluginLoadStatus.FailedToLoad && row.Diagnostic!.Contains("missing from package"));
    }

    [Fact]
    public void ConstructorFailureDoesNotBlockIndependentPackage()
    {
        Package("bad", "sample.bad", source: PluginSource("Bad", constructor: "throw new System.InvalidOperationException(\"fixture constructor failed\");"));
        Package("good", "sample.good");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Contains(host.Discovery, row => row.Status == PluginLoadStatus.FailedToLoad && row.Diagnostic!.Contains("fixture constructor failed"));
    }

    [Fact]
    public void EscapingDirectoryLinkIsRejected()
    {
        var entry = Package("bad", "sample.bad");
        var link = Path.Combine(Path.GetDirectoryName(entry)!, "escape");
        CreateDirectoryLink(link, _root);
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal(PluginLoadStatus.InvalidPackage, Assert.Single(host.Discovery).Status);
        Assert.Empty(host.Plugins);
    }

    [Fact]
    public void MissingNativeDependencyFailsOnlyItsPackage()
    {
        var source = PluginSource("Native", "NativeApi.GetValue().ToString()") + """
            public static class NativeApi
            {
                [System.Runtime.InteropServices.DllImport("gondwana_fixture_missing_native")]
                public static extern int GetValue();
            }
            """;
        Package("bad", "sample.bad", source: source);
        Package("good", "sample.good");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Contains(host.Discovery, row => row.Status == PluginLoadStatus.FailedToLoad && row.Diagnostic!.Contains("Native dependency"));
    }

    [Fact]
    public void CorruptEntryAssemblyDoesNotBlockHealthyPackage()
    {
        var entry = Package("bad", "sample.bad");
        File.WriteAllText(entry, "not a managed assembly");
        Package("good", "sample.good");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Contains(host.Discovery, row => row.Id == "sample.bad" && row.Status == PluginLoadStatus.FailedToLoad);
    }

    [Fact]
    public void ConstructorFailureDoesNotHideOtherImplementationsInSamePackage()
    {
        Package("mixed", "sample.mixed", source: PluginSource("Bad", constructor: "throw new System.Exception(\"fixture\");") + PluginSource("Good"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Equal(2, host.Discovery.Count);
        Assert.Contains(host.Discovery, row => row.Implementation == "Bad" && row.Status == PluginLoadStatus.FailedToLoad);
        Assert.Contains(host.Discovery, row => row.Implementation == "Good" && row.Status == PluginLoadStatus.Loaded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrameworkCatalogUsesDeploymentMetadataRatherThanAssemblyNamePrefixes(bool selfContained)
    {
        Directory.CreateDirectory(_root);
        var frameworkLibraries = new Dictionary<string, object>
        {
            ["runtimepack.Microsoft.NETCore.App.Runtime.win-x64/8.0.0"] = new
            {
                runtime = new Dictionary<string, object> { ["System.Runtime.dll"] = new { }, ["Microsoft.Win32.Registry.dll"] = new { } }
            },
            ["runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/8.0.0"] = new
            {
                runtime = new Dictionary<string, object> { ["WindowsFormsIntegration.dll"] = new { } }
            }
        };
        var applicationLibraries = new Dictionary<string, object>
        {
            ["System.PrivateApplicationLibrary/1.0.0"] = new
            {
                runtime = new Dictionary<string, object> { ["System.PrivateApplicationLibrary.dll"] = new { } }
            }
        };
        var frameworkFile = Path.Combine(_root, "Microsoft.WindowsDesktop.App.deps.json");
        var applicationFile = Path.Combine(_root, "Application.deps.json");
        if (selfContained)
            foreach (var library in frameworkLibraries) applicationLibraries.Add(library.Key, library.Value);
        else WriteDeps(frameworkFile, frameworkLibraries);
        WriteDeps(applicationFile, applicationLibraries);
        var catalog = FrameworkAssemblyCatalog.Read(selfContained ? [applicationFile] : [frameworkFile, applicationFile]);
        Assert.Contains("WindowsFormsIntegration", catalog);
        Assert.Contains("Microsoft.Win32.Registry", catalog);
        Assert.Contains("System.Runtime", catalog);
        Assert.DoesNotContain("System.PrivateApplicationLibrary", catalog);
    }

    [Fact]
    public void RuntimeCatalogIncludesEnabledFrameworks()
    {
        var catalog = FrameworkAssemblyCatalog.ReadRuntime();
        Assert.Contains("System.Runtime", catalog);
        // This test project enables Windows Desktop; it must authorize framework
        // assemblies beyond those already referenced by the Studio host itself.
        if (OperatingSystem.IsWindows()) Assert.Contains("WindowsFormsIntegration", catalog);
    }

    [Fact]
    public void PackagedPluginCanReferenceUnsharedWindowsDesktopAssembly()
    {
        if (!OperatingSystem.IsWindows()) return;
        var windowsForms = System.Reflection.Assembly.Load("System.Windows.Forms").Location;
        var integration = Path.Combine(Path.GetDirectoryName(windowsForms)!, "WindowsFormsIntegration.dll");
        Package("desktop", "sample.desktop", source: PluginSource("Desktop", "typeof(System.Windows.Forms.Integration.ElementHost).FullName!"), dependency: integration);
        // The Core-only host does not share WinForms or WPF contracts. Framework
        // metadata must allow this reference without a private framework copy.
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal("System.Windows.Forms.Integration.ElementHost", Assert.Single(host.Plugins).Name);
    }

    [Fact]
    public void LinkedChildDoesNotHideDuplicateManifestId()
    {
        var entry = Package("invalid", "sample.same");
        CreateDirectoryLink(Path.Combine(Path.GetDirectoryName(entry)!, "escape"), _root);
        Package("valid", "sample.same");
        Package("independent", "sample.independent");
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Equal(2, host.Discovery.Count(row => row.Id == "sample.same" && row.Status == PluginLoadStatus.Conflict));
    }

    [Fact]
    public void AssemblyNameCasingDoesNotAllowDuplicateImplementations()
    {
        Package("first", "sample.first", "ExamplePlugin", PluginSource("SameType"));
        Package("second", "sample.second", "exampleplugin", PluginSource("SameType"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Single(host.Plugins);
        Assert.Single(host.Discovery, row => row.Status == PluginLoadStatus.Conflict);
    }

    [Fact]
    public void ImplementationTypeNamesRemainCaseSensitive()
    {
        Package("first", "sample.first", "ExamplePlugin", PluginSource("ExampleType"));
        Package("second", "sample.second", "exampleplugin", PluginSource("exampletype"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(_root);
        Assert.Equal(2, host.Plugins.Count);
        Assert.All(host.Discovery, row => Assert.Equal(PluginLoadStatus.Loaded, row.Status));
    }

    [Fact]
    public void LegacyDependencyLibrariesAreNotReportedAsFailedPlugins()
    {
        Directory.CreateDirectory(_root);
        var dependency = Path.Combine(_root, "LegacyHelper.dll");
        Compile(dependency, "public static class Helper { public static string Value => \"legacy-helper\"; }");
        Compile(Path.Combine(_root, "Legacy.dll"), PluginSource("Legacy", "Helper.Value"), dependency);
        var messages = new List<string>();
        var host = new Host(messages.Add);
        host.DiscoverAndLoad(_root);
        host.DiscoverAndLoad(_root);
        Assert.Equal("legacy-helper", Assert.Single(host.Plugins).Name);
        Assert.Equal(PluginLoadStatus.Loaded, Assert.Single(host.Discovery).Status);
        Assert.DoesNotContain(messages, message => message.StartsWith("Error:", StringComparison.Ordinal));
    }

    private static void WriteDeps(string path, Dictionary<string, object> libraries)
        => File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            targets = new Dictionary<string, object> { [".NETCoreApp,Version=v8.0/win-x64"] = libraries }
        }));

    private static void CreateDirectoryLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            // Directory junction creation does not require the symbolic-link privilege.
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, target);
    }

    private string Package(string folder, string id, string? assembly = null, string? source = null, string? dependency = null)
    {
        assembly ??= "Fixture" + folder;
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        var entry = Path.Combine(directory, assembly + ".dll");
        Compile(entry, source ?? PluginSource(assembly), dependency);
        Manifest(directory, id, Path.GetFileName(entry));
        return entry;
    }

    private static void Manifest(string directory, string id, string entry)
        => File.WriteAllText(Path.Combine(directory, "plugin.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            id,
            name = "Fixture package",
            version = "1.0.0",
            entryAssembly = entry,
            contractVersion = 1
        }));

    private static void DependencyManifest(string directory, string assembly, int version)
    {
        var library = $"PrivateHelper/{version}.0.0";
        File.WriteAllText(Path.Combine(directory, assembly + ".deps.json"), JsonSerializer.Serialize(new
        {
            runtimeTarget = new { name = ".NETCoreApp,Version=v8.0", signature = "" },
            targets = new Dictionary<string, object>
            {
                [".NETCoreApp,Version=v8.0"] = new Dictionary<string, object>
                {
                    [assembly + "/1.0.0"] = new { dependencies = new Dictionary<string, string> { ["PrivateHelper"] = $"{version}.0.0" }, runtime = new Dictionary<string, object> { [assembly + ".dll"] = new { } } },
                    [library] = new { runtime = new Dictionary<string, object> { ["PrivateHelper.dll"] = new { assemblyVersion = $"{version}.0.0.0" } } }
                }
            },
            libraries = new Dictionary<string, object>
            {
                [assembly + "/1.0.0"] = new { type = "project", serviceable = false, sha512 = "" },
                [library] = new { type = "project", serviceable = false, sha512 = "" }
            }
        }));
    }

    private static string PluginSource(string type, string name = "\"Fixture\"", string constructor = "") => $$"""
        public sealed class {{type}} : Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin
        {
            public {{type}}() { {{constructor}} }
            public string Name => {{name}};
            public void OnProjectOpened(string path) { if (path == "throw") throw new System.InvalidOperationException("lifecycle"); }
            public void OnProjectClosed() { }
        }
        """;

    private static void Compile(string path, string source, string? dependency = null)
    {
        var references = dependency is null ? References : References.Append(MetadataReference.CreateFromFile(dependency));
        var compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(path),
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }
}
