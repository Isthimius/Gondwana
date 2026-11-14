using System.IO.Compression;
using System.Text.Json;
using Gondwana.Tooling.Studio.Core.Extensibility;
using Host = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class PluginPackageTests
{
    private string InstallRoot => Path.Combine(_root, "installation", "plugins");
    private PluginPackageOperations Operations() => new(InstallRoot, _ => { });
    private string SourcePackage(string folder = "source", string id = "sample.package", string version = "1.0.0")
    {
        var directory = Path.GetDirectoryName(Package(folder, id))!;
        SetManifest(directory, "version", version);
        return directory;
    }
    private static void SetManifest(string directory, string key, object value)
    {
        var path = Path.Combine(directory, "plugin.json");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path))!;
        manifest[key] = value;
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
    }
    private string InstallFixture(string version = "1.0.0")
    {
        var source = SourcePackage(version: version);
        using (var service = Operations()) service.Queue(service.Preview(source), true);
        using (var startup = Operations()) { startup.StartSession(); Assert.Empty(startup.Operations); }
        return Path.Combine(InstallRoot, "package-sample.package");
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("zip")]
    [InlineData("wrapped")]
    public void LocalPackagePreviewQueuesWithoutConstructionAndAppliesBeforeDiscovery(string format)
    {
        var marker = Path.Combine(_root, "constructed.txt");
        var source = Path.GetDirectoryName(Package("source", "sample.package", source: PluginSource("PreviewPlugin", constructor: $"System.IO.File.WriteAllText({JsonSerializer.Serialize(marker)}, \"constructed\");")))!;
        var input = source;
        if (format != "directory")
        {
            input = Path.Combine(_root, "plugin.zip");
            ZipFile.CreateFromDirectory(source, input, CompressionLevel.Fastest, includeBaseDirectory: format == "wrapped");
        }
        using (var service = Operations())
        {
            service.StartSession();
            var preview = service.Preview(input);
            Assert.Equal("sample.package", preview.Manifest.Id);
            Assert.False(File.Exists(marker));
            Assert.False(Directory.Exists(InstallRoot));
            Assert.Throws<InvalidOperationException>(() => service.Queue(preview, false));
            service.Queue(preview, true);
            Assert.True(service.RestartRequired);
            var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
            Assert.Empty(host.Plugins);
            Assert.False(File.Exists(marker));
        }
        using var next = Operations();
        next.StartSession();
        Assert.Empty(next.Operations);
        var loaded = new Host(_ => { }); loaded.DiscoverAndLoad(InstallRoot);
        Assert.Single(loaded.Plugins);
        Assert.True(File.Exists(marker));
    }

    [Theory]
    [InlineData("2.0.0", "Upgrade", false)]
    [InlineData("1.0.0", "Same-version replacement", true)]
    [InlineData("0.9.0-beta.1", "Downgrade", true)]
    public void UpdateReplacesExactlyAndPreservesActivation(string version, string change, bool needsExtraConsent)
    {
        var target = InstallFixture();
        File.WriteAllText(Path.Combine(target, "obsolete.txt"), "old");
        var prefsPath = Path.Combine(_root, "preferences.json");
        var preferences = new PluginPreferences(prefsPath, _ => { }); preferences.TrySetEnabled("sample.package", false, out _);
        var replacement = SourcePackage("replacement", version: version);
        using (var service = Operations())
        {
            service.StartSession();
            var preview = service.Preview(replacement, "sample.package");
            Assert.Equal(change, preview.VersionChange);
            if (needsExtraConsent) Assert.Throws<InvalidOperationException>(() => service.Queue(preview, true));
            service.Queue(preview, true, replacementConfirmed: true);
            Assert.True(File.Exists(Path.Combine(target, "obsolete.txt")));
            Assert.Equal("1.0.0", PluginManifest.Read(target).Version);
        }
        using var next = Operations(); next.StartSession();
        Assert.Empty(next.Operations);
        Assert.Equal(version, PluginManifest.Read(target).Version);
        Assert.False(File.Exists(Path.Combine(target, "obsolete.txt")));
        Assert.False(new PluginPreferences(prefsPath, _ => { }).IsEnabled("sample.package"));
        var host = new Host(_ => { }, new PluginPreferences(prefsPath, _ => { }).WasEnabledAtStartup); host.DiscoverAndLoad(InstallRoot);
        Assert.Empty(host.Plugins);
        Assert.Equal(PluginLoadStatus.UserDisabled, Assert.Single(host.Discovery).Status);
    }

    [Theory]
    [InlineData("1.0.0-alpha.9", "1.0.0-alpha.10", -1)]
    [InlineData("1.0.0-beta", "1.0.0-9", 1)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0+one", "1.0.0+two", 0)]
    [InlineData("99999999999999999999.0.0", "2.0.0", 1)]
    public void PackageVersionComparisonUsesSemVer(string left, string right, int expected)
        => Assert.Equal(expected, Math.Sign(PluginPackageOperations.CompareVersions(left, right)));

    [Theory]
    [InlineData("before-backup")]
    [InlineData("after-backup")]
    [InlineData("after-publish")]
    public void FailedReplacementRollsBackAndDoesNotRetry(string checkpoint)
    {
        var target = InstallFixture();
        var original = PluginPackageInspection.Fingerprint(target);
        using (var queue = Operations()) queue.Queue(queue.Preview(SourcePackage("replacement", version: "2.0.0")), true);
        using (var next = Operations())
        {
            next.Checkpoint = point => { if (point == checkpoint) throw new IOException("Injected move failure"); };
            next.StartSession();
            Assert.Equal(original, PluginPackageInspection.Fingerprint(target));
            Assert.Equal(PluginPackageOperationState.Failed, Assert.Single(next.Operations).State);
        }
        using var restart = Operations(); restart.StartSession();
        Assert.Equal(original, PluginPackageInspection.Fingerprint(target));
        Assert.Equal(PluginPackageOperationState.Failed, Assert.Single(restart.Operations).State);
        restart.Cancel("sample.package");
        Assert.Empty(restart.Operations);
    }

    [Fact]
    public void InterruptedTransactionRestoresBackupWithoutTrustingStage()
    {
        var target = InstallFixture();
        using (var queue = Operations())
        {
            var preview = queue.Preview(SourcePackage("replacement", version: "2.0.0")); queue.Queue(preview, true);
            var journalPath = Path.Combine(queue.WorkRoot, "operations.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
            json["Operations"]![0]!["State"] = (int)PluginPackageOperationState.Applying;
            File.WriteAllText(journalPath, json.ToJsonString());
            Directory.Move(target, Path.Combine(queue.WorkRoot, preview.Operation.Token, "backup"));
        }
        using var startup = Operations(); startup.StartSession();
        Assert.Equal("1.0.0", PluginManifest.Read(target).Version);
        Assert.Equal(PluginPackageOperationState.Failed, Assert.Single(startup.Operations).State);
    }

    [Fact]
    public void MultipleInstancesDeferUntilAllReadersExit()
    {
        var source = SourcePackage();
        using (var first = Operations())
        {
            first.StartSession(); first.Queue(first.Preview(source), true);
            using var second = Operations(); second.StartSession();
            Assert.True(second.CanDiscover);
            Assert.Contains("another Studio instance", second.Diagnostic);
            Assert.True(second.RestartRequired);
            Assert.False(Directory.Exists(InstallRoot));
        }
        using var last = Operations(); last.StartSession();
        Assert.Empty(last.Operations);
        Assert.True(Directory.Exists(Path.Combine(InstallRoot, "package-sample.package")));
    }

    [Fact]
    public void StaleDestinationFailsWithoutBlockingAnotherInstall()
    {
        var target = InstallFixture();
        using (var queue = Operations())
        {
            queue.Queue(queue.Preview(SourcePackage("replacement", version: "2.0.0")), true);
            queue.Queue(queue.Preview(SourcePackage("other", "sample.other")), true);
        }
        File.WriteAllText(Path.Combine(target, "external.txt"), "preserve");
        using var next = Operations(); next.StartSession();
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(target, "external.txt")));
        Assert.Equal("1.0.0", PluginManifest.Read(target).Version);
        Assert.Equal(PluginPackageOperationState.Failed, Assert.Single(next.Operations).State);
        Assert.True(Directory.Exists(Path.Combine(InstallRoot, "package-sample.other")));
    }

    [Fact]
    public void RemovalPreservesPreferencesAndUnrelatedPackages()
    {
        var target = InstallFixture();
        var prefs = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { }); prefs.TrySetEnabled("sample.package", false, out _);
        var other = Path.Combine(InstallRoot, "unrelated"); Directory.CreateDirectory(other); File.WriteAllText(Path.Combine(other, "keep.txt"), "keep");
        using (var queue = Operations()) { var preview = queue.PreviewRemoval("sample.package"); queue.Queue(preview, true); Assert.True(Directory.Exists(target)); }
        using var next = Operations(); next.StartSession();
        Assert.False(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(other, "keep.txt")));
        Assert.False(new PluginPreferences(prefs.FilePath, _ => { }).IsEnabled("sample.package"));
        Assert.Throws<InvalidDataException>(() => next.PreviewRemoval("sample.package"));
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot); Assert.Empty(host.Plugins);
    }

    [Fact]
    public void PendingUpdateCanBeReplacedButContradictoryOperationsRequireCancel()
    {
        InstallFixture();
        using var service = Operations();
        var first = service.Preview(SourcePackage("v2", version: "2.0.0")); service.Queue(first, true);
        var second = service.Preview(SourcePackage("v3", version: "3.0.0")); service.Queue(second, true);
        Assert.Equal("3.0.0", Assert.Single(service.Operations).Version);
        Assert.False(Directory.Exists(Path.Combine(service.WorkRoot, first.Operation.Token)));
        var removal = service.PreviewRemoval("sample.package");
        Assert.Throws<InvalidOperationException>(() => service.Queue(removal, true));
        service.Cancel("sample.package"); service.Queue(removal, true);
        service.Cancel("sample.package"); Assert.Empty(service.Operations);
    }

    [Fact]
    public void StagedMutationAndUnrelatedUpdateAreRejected()
    {
        InstallFixture();
        using var service = Operations();
        Assert.Throws<InvalidDataException>(() => service.Preview(SourcePackage("other", "sample.other"), "sample.package"));
        var preview = service.Preview(SourcePackage("replacement", version: "2.0.0"));
        File.WriteAllText(Path.Combine(service.WorkRoot, preview.Operation.Token, "payload", "new.txt"), "changed");
        Assert.Throws<InvalidDataException>(() => service.Queue(preview, true));
        service.Discard(preview);
    }

    [Theory]
    [InlineData("gondwana.project-diagnostics")]
    [InlineData("gondwana.external-import")]
    public void BundledPackagesAreProtected(string id)
    {
        var source = SourcePackage(id: id);
        using var service = Operations();
        Assert.Throws<InvalidDataException>(() => service.Preview(source));
        Directory.CreateDirectory(InstallRoot);
        PluginPackageInspection.CopyDirectory(source, Path.Combine(InstallRoot, "bundled"));
        Assert.Throws<InvalidDataException>(() => service.PreviewRemoval(id));
    }

    [Theory]
    [InlineData("../escape.dll")]
    [InlineData("/absolute.dll")]
    [InlineData("C:/drive.dll")]
    [InlineData("folder\\escape.dll")]
    [InlineData("CON.dll")]
    [InlineData("folder./bad.dll")]
    public void ArchiveUnsafePathsAreRejected(string name)
    {
        Directory.CreateDirectory(_root);
        var zip = Path.Combine(_root, "unsafe.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) archive.CreateEntry(name);
        using var service = Operations(); Assert.ThrowsAny<Exception>(() => service.Preview(zip));
        Assert.False(Directory.Exists(InstallRoot));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("link")]
    [InlineData("count")]
    [InlineData("corrupt")]
    [InlineData("nested")]
    public void UnsafeOrMalformedArchivesAreRejected(string reason)
    {
        Directory.CreateDirectory(_root); var zip = Path.Combine(_root, "invalid.zip");
        if (reason == "corrupt") File.WriteAllText(zip, "not a zip");
        else using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                if (reason == "duplicate") { archive.CreateEntry("plugin.json"); archive.CreateEntry("PLUGIN.JSON"); }
                if (reason == "link") archive.CreateEntry("link").ExternalAttributes = unchecked((int)0xA1FF0000);
                if (reason == "count") for (var i = 0; i <= PluginPackageInspection.MaximumEntries; i++) archive.CreateEntry("file" + i);
                if (reason == "nested") archive.CreateEntry("one/two/plugin.json");
            }
        using var service = Operations(); Assert.ThrowsAny<Exception>(() => service.Preview(zip));
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("entry")]
    [InlineData("schema")]
    [InlineData("contract")]
    [InlineData("host")]
    public void InstallerReusesManifestAndAssemblyValidation(string reason)
    {
        var source = SourcePackage();
        if (reason == "manifest") File.Delete(Path.Combine(source, "plugin.json"));
        if (reason == "entry") File.Delete(Path.Combine(source, "Fixturesource.dll"));
        if (reason == "schema") SetManifest(source, "schemaVersion", 99);
        if (reason == "contract") SetManifest(source, "contractVersion", 99);
        if (reason == "host") File.Copy(typeof(IStudioPlugin).Assembly.Location, Path.Combine(source, "renamed.dll"));
        using var service = Operations(); Assert.ThrowsAny<Exception>(() => service.Preview(source));
    }

    [Fact]
    public void DuplicateInstalledIdsAndOccupiedDestinationsAreRejected()
    {
        var target = InstallFixture();
        PluginPackageInspection.CopyDirectory(target, Path.Combine(InstallRoot, "duplicate"));
        using var service = Operations();
        Assert.Throws<InvalidDataException>(() => service.PreviewRemoval("sample.package"));
        Assert.Throws<InvalidDataException>(() => service.Preview(SourcePackage("replacement")));
        Directory.CreateDirectory(Path.Combine(InstallRoot, "package-sample.other"));
        Assert.Throws<InvalidDataException>(() => service.Preview(SourcePackage("other", "sample.other")));
    }

    [Fact]
    public void CorruptOrUnsafeJournalCannotMutateInstalledFiles()
    {
        var target = InstallFixture();
        using var service = Operations();
        File.WriteAllText(Path.Combine(service.WorkRoot, "operations.json"), "{broken");
        service.StartSession();
        Assert.True(service.CanDiscover);
        Assert.Contains("journal", service.Diagnostic);
        Assert.True(Directory.Exists(target));
        Assert.ThrowsAny<Exception>(() => service.PreviewRemoval("../plugins"));
    }
    [Fact]
    public void ExpandedArchiveLimitIsEnforcedBeforeExtraction()
    {
        Directory.CreateDirectory(_root);
        var zip = Path.Combine(_root, "large.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var stream = archive.CreateEntry("large.bin", CompressionLevel.Fastest).Open())
        {
            var block = new byte[1024 * 1024];
            for (var i = 0; i < 257; i++) stream.Write(block);
        }
        using var service = Operations();
        Assert.Throws<InvalidDataException>(() => service.Preview(zip));
        Assert.False(Directory.Exists(InstallRoot));
    }

    [Fact]
    public void InstallerChecksPrivateManagedAndDeclaredNativeDependencies()
    {
        var source = SourcePackage();
        var helper = Path.Combine(source, "PrivateHelper.dll");
        Compile(helper, "public static class Helper { public static string Name => \"Private\"; }");
        Compile(Path.Combine(source, "Fixturesource.dll"), PluginSource("PrivatePlugin", name: "Helper.Name"), helper);
        DependencyManifest(source, "Fixturesource", 1);
        using var service = Operations();
        var preview = service.Preview(source);
        Assert.Contains("PrivateHelper.dll", preview.Dependencies);
        service.Discard(preview);
        File.Delete(helper);
        Assert.Contains("PrivateHelper", Assert.Throws<InvalidDataException>(() => service.Preview(source)).Message);
        Compile(helper, "public static class Helper { public static string Name => \"Private\"; }");
        var deps = Path.Combine(source, "native.deps.json");
        File.WriteAllText(deps, """{"targets":{"target":{"lib/1.0":{"native":{"missing-native.dll":{}}}}}}""");
        Assert.Contains("missing-native", Assert.Throws<InvalidDataException>(() => service.Preview(source)).Message);
    }

    [Fact]
    public void DirectoryLinksAreRejectedWithoutFollowingThem()
    {
        var source = SourcePackage();
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        CreateDirectoryLink(Path.Combine(source, "link"), outside);
        using var service = Operations(); Assert.Throws<InvalidDataException>(() => service.Preview(source));
        Assert.True(Directory.Exists(outside));
    }

    [Fact]
    public void InstallerDoesNotExecuteModuleInitializers()
    {
        var marker = Path.Combine(_root, "module.txt");
        var source = Path.GetDirectoryName(Package("module", "sample.module", source: PluginSource("ModulePlugin") +
            $"public static class Initializer {{ [System.Runtime.CompilerServices.ModuleInitializer] public static void Init() {{ System.IO.File.WriteAllText({JsonSerializer.Serialize(marker)}, \"executed\"); }} }}"))!;
        using var service = Operations();
        var preview = service.Preview(source); service.Queue(preview, true);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void MissingDestinationAtStartupAndTamperedRecoveryDataArePreserved()
    {
        var target = InstallFixture();
        using (var queue = Operations()) queue.Queue(queue.PreviewRemoval("sample.package"), true);
        Directory.Move(target, Path.Combine(_root, "externallyMoved"));
        using (var startup = Operations())
        {
            startup.StartSession();
            Assert.Equal(PluginPackageOperationState.Failed, Assert.Single(startup.Operations).State);
            startup.Cancel("sample.package");
        }
        Directory.Move(Path.Combine(_root, "externallyMoved"), target);
        using (var queue = Operations())
        {
            var preview = queue.Preview(SourcePackage("replacement", version: "2.0.0")); queue.Queue(preview, true);
            var journalPath = Path.Combine(queue.WorkRoot, "operations.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
            json["Operations"]![0]!["State"] = (int)PluginPackageOperationState.Applying;
            File.WriteAllText(journalPath, json.ToJsonString());
            var backup = Path.Combine(queue.WorkRoot, preview.Operation.Token, "backup");
            Directory.Move(target, backup);
            File.WriteAllText(Path.Combine(backup, "user-data.txt"), "preserve");
        }
        using var recovery = Operations(); recovery.StartSession();
        var failed = Assert.Single(recovery.Operations);
        Assert.Equal(PluginPackageOperationState.RecoveryRequired, failed.State);
        Assert.True(File.Exists(Path.Combine(recovery.WorkRoot, failed.Token, "backup", "user-data.txt")));
        Assert.Throws<InvalidOperationException>(() => recovery.Cancel(failed.PluginId));
    }

    [Fact]
    public void CancellationRetainsUnexpectedStageContentAndNeverDeletesUserData()
    {
        using var service = Operations();
        var preview = service.Preview(SourcePackage()); service.Queue(preview, true);
        var data = Path.Combine(service.WorkRoot, preview.Operation.Token, "payload", "user.txt");
        File.WriteAllText(data, "preserve");
        service.Cancel("sample.package");
        Assert.Empty(service.Operations);
        Assert.True(File.Exists(data));
    }

    [Fact]
    public void UnwritableCoordinationLocationDoesNotCrashStudioOrMutatePlugins()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InstallRoot)!);
        using var service = Operations();
        File.WriteAllText(service.WorkRoot, "obstruction");
        service.StartSession();
        Assert.False(service.CanDiscover);
        Assert.Contains("permissions", service.Diagnostic);
        Assert.Equal("obstruction", File.ReadAllText(service.WorkRoot));
    }

    [Fact]
    public void SourceCopyFailureNeverQueuesOrTouchesAnInstallation()
    {
        var source = SourcePackage();
        using var service = Operations();
        using var locked = new FileStream(Path.Combine(source, "Fixturesource.dll"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.ThrowsAny<IOException>(() => service.Preview(source));
        Assert.Empty(service.Operations);
        Assert.False(Directory.Exists(InstallRoot));
    }

    [Fact]
    public void QueuedUpdateLeavesCurrentInstancesAndFilesUnchanged()
    {
        var target = InstallFixture();
        using var service = Operations(); service.StartSession();
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
        var instance = Assert.Single(host.Plugins);
        var replacement = SourcePackage("replacement", version: "2.0.0");
        service.Queue(service.Preview(replacement), true);
        host.DiscoverAndLoad(InstallRoot);
        Assert.Same(instance, Assert.Single(host.Plugins));
        Assert.Equal("1.0.0", PluginManifest.Read(target).Version);
        Assert.True(service.RestartRequired);
    }

    [Fact]
    public void NextStartupLoadsReplacementImplementation()
    {
        InstallFixture();
        var replacement = Path.GetDirectoryName(Package("replacement", "sample.package", source: PluginSource("UpdatedPlugin", name: "\"Updated implementation\"")))!;
        SetManifest(replacement, "version", "2.0.0");
        using (var queue = Operations()) queue.Queue(queue.Preview(replacement), true);
        using var startup = Operations(); startup.StartSession();
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
        Assert.Equal("Updated implementation", Assert.Single(host.Plugins).Name);
        Assert.Equal("2.0.0", Assert.Single(host.Discovery).Version);
    }

    [Fact]
    public void CorruptArchivePayloadCrcIsRejected()
    {
        var source = SourcePackage();
        File.WriteAllText(Path.Combine(source, "support.txt"), "crc-check-data");
        var zip = Path.Combine(_root, "crc.zip");
        ZipFile.CreateFromDirectory(source, zip, CompressionLevel.NoCompression, false);
        var bytes = File.ReadAllBytes(zip);
        var marker = System.Text.Encoding.UTF8.GetBytes("crc-check-data");
        var index = Enumerable.Range(0, bytes.Length - marker.Length).First(i => bytes.AsSpan(i, marker.Length).SequenceEqual(marker));
        bytes[index] ^= 1;
        File.WriteAllBytes(zip, bytes);
        using var service = Operations();
        Assert.Contains("CRC", Assert.Throws<InvalidDataException>(() => service.Preview(zip)).Message);
        Assert.False(Directory.Exists(InstallRoot));
    }

}
