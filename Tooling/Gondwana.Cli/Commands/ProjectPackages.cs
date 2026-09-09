using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Gondwana.Cli.Commands;

// Deliberately inspects literal XML without executing user MSBuild targets. Complex
// imports/conditions are reportable, but must never be guessed when editing.
internal sealed class ProjectPackages
{
    internal sealed record Package(string Name, XElement Reference, XObject? VersionNode, string? Version);
    private readonly Dictionary<string, XDocument> documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> originals = new(StringComparer.OrdinalIgnoreCase);
    public string Path { get; }
    public XDocument Project { get; }
    public XDocument? Central { get; }
    public bool CentrallyManaged { get; }
    public List<Package> Packages { get; } = [];
    public List<string> References { get; }
    public string? Host { get; }
    public List<string> Limitations { get; } = [];

    public static bool IsGondwana(string name) => name.Equals("Gondwana", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Gondwana.", StringComparison.OrdinalIgnoreCase);
    public static string Name(XElement element) => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? "";
    public static bool Conditional(XElement element) => element.AncestorsAndSelf().Any(e => e.Attribute("Condition") is not null || e.Name.LocalName is "Choose" or "Target");
    public static bool Literal(string value) => !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(['$', '@', '%', '*', '?', ';']) < 0;
    public static bool ExactVersion(string? value) => value is not null && Regex.IsMatch(value, @"^\d+\.\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$");

    public ProjectPackages(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Project = Load(Path);
        foreach (var file in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            var found = FindAncestorFile(System.IO.Path.GetDirectoryName(Path)!, file);
            if (found is not null) Load(found);
        }
        Central = documents.FirstOrDefault(d => System.IO.Path.GetFileName(d.Key).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)).Value;
        var management = Elements("ManagePackageVersionsCentrally").ToArray();
        CentrallyManaged = management.Length == 1 && !Conditional(management[0]) && management[0].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (management.Length > 1 || management.Any(Conditional) || management.Any(e => !new[] { "true", "false" }.Contains(e.Value, StringComparer.OrdinalIgnoreCase)))
            Limitations.Add("Ambiguous central package management settings.");
        if (documents.Values.Any(d => d.Descendants().Any(e => e.Name.LocalName is "Import" or "ImportGroup")))
            Limitations.Add("Explicit MSBuild imports require manual review before package edits.");
        if (documents.Values.Any(d => d.Descendants().Any(e => e.Name.LocalName is "DirectoryBuildPropsPath" or "DirectoryBuildTargetsPath" or "DirectoryPackagesPropsPath" or "ImportDirectoryBuildProps" or "ImportDirectoryBuildTargets" or "ImportDirectoryPackagesProps")))
            Limitations.Add("Custom MSBuild directory import settings require manual review before package edits.");
        if (Elements("PackageReference").Any(e => e.Document != Project && IsGondwana(Name(e))))
            Limitations.Add("Imported Gondwana PackageReference items require manual edits in their owning file.");
        foreach (var reference in Project.Descendants().Where(e => e.Name.LocalName == "PackageReference" && IsGondwana(Name(e))))
        {
            XObject? node = VersionNode(reference, "VersionOverride") ?? VersionNode(reference, "Version");
            if (node is null && CentrallyManaged)
            {
                var matches = Central?.Descendants().Where(e => e.Name.LocalName == "PackageVersion" && Name(e).Equals(Name(reference), StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
                if (matches.Length == 1 && !Conditional(matches[0])) node = VersionNode(matches[0], "Version");
            }
            node = ResolveProperty(node);
            Packages.Add(new(Name(reference), reference, node, Value(node)));
        }
        References = Project.Descendants().Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => System.IO.Path.GetFileNameWithoutExtension(Name(e).Replace('\\', '/'))).Where(IsGondwana).ToList();
        var names = Packages.Select(p => p.Name).Concat(References).ToArray();
        var hosts = new[] { "WinForms", "Avalonia", "Blazor" }.Where(h => names.Any(n => n.Equals($"Gondwana.{h}", StringComparison.OrdinalIgnoreCase) || n.Equals($"Gondwana.{h}.Hosting", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (hosts.Length == 0 && ProjectHelper.IsBlazorWebAssemblyProject(Path)) hosts = ["Blazor"];
        if (hosts.Length == 0 && Project.Descendants().Any(e => e.Name.LocalName == "UseWindowsForms" && !Conditional(e) && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase))) hosts = ["WinForms"];
        Host = hosts.Length == 1 ? hosts[0] : null;
        if (hosts.Length > 1) Limitations.Add("Multiple Gondwana platform adapters; host selection is ambiguous.");
    }

    private XDocument Load(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        documents.Add(path, doc);
        originals.Add(path, File.ReadAllBytes(path));
        return doc;
    }

    internal static string? FindAncestorFile(string directory, string name)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var candidate = System.IO.Path.Combine(current.FullName, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private IEnumerable<XElement> Elements(string name) => documents.Values.SelectMany(d => d.Descendants().Where(e => e.Name.LocalName == name));
    private static XObject? VersionNode(XElement element, string name) => (XObject?)element.Attribute(name) ?? element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
    private static string? Value(XObject? node) => node is XAttribute a ? a.Value : (node as XElement)?.Value;
    private XObject? ResolveProperty(XObject? node)
    {
        var value = Value(node);
        if (value is null || !value.Contains('$')) return node;
        var match = Regex.Match(value, @"^\$\((\w+)\)$");
        if (!match.Success) return null;
        var candidates = Elements(match.Groups[1].Value).Where(e => e.Parent?.Name.LocalName == "PropertyGroup").ToArray();
        return candidates.Length == 1 && !Conditional(candidates[0]) && ExactVersion(candidates[0].Value) ? candidates[0] : null;
    }

    public string AlignedVersion()
    {
        EnsureEditable();
        var versions = Packages.Select(p => p.Version).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (versions.Length != 1 || !ExactVersion(versions[0]))
            throw new InvalidOperationException("Gondwana package versions are not aligned and explicit. Run upgrade --version <version> or resolve versions manually first.");
        return versions[0]!;
    }

    public void EnsureEditable()
    {
        if (Packages.Count == 0) throw new InvalidOperationException("No Gondwana NuGet references found. Project-reference-only games must manage versions in their source repository.");
        if (References.Count != 0) throw new InvalidOperationException("Mixed Gondwana package/project references require manual review before editing.");
        if (Limitations.Count > 0) throw new InvalidOperationException(string.Join(" ", Limitations));
        if (Packages.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) || Packages.Any(p => Conditional(p.Reference) || p.Reference.Elements().Any(Conditional) || p.Reference.Attribute("Include") is null || p.VersionNode is null || !ExactVersion(p.Version)))
            throw new InvalidOperationException("Conditional, duplicate, Update-only, or unresolved Gondwana versions require manual review. No files changed.");
        if (Packages.Any(p => new[] { "Version", "VersionOverride" }.Any(name => p.Reference.Attributes(name).Count() + p.Reference.Elements().Count(e => e.Name.LocalName == name) > 1)))
            throw new InvalidOperationException("Duplicate package version metadata requires manual review.");
        foreach (var package in Packages)
        {
            if (package.VersionNode is XElement property && property.Parent?.Name.LocalName == "PropertyGroup")
            {
                var expression = "$(" + property.Name.LocalName + ")";
                var uses = documents.Values.SelectMany(d => d.Descendants()).Where(e => e.Attributes().Any(a => a.Value.Contains(expression)) || (!e.HasElements && e.Value.Contains(expression)));
                if (uses.Any(e => !e.AncestorsAndSelf().Any(a => (a.Name.LocalName is "PackageReference" or "PackageVersion") && Packages.Any(p => p.Name.Equals(Name(a), StringComparison.OrdinalIgnoreCase)))))
                    throw new InvalidOperationException($"Property {property.Name.LocalName} also controls unrelated content; edit it manually.");
            }
        }
    }

    public IReadOnlyList<string> Upgrade(string version)
    {
        EnsureEditable();
        if (!ExactVersion(version)) throw new InvalidOperationException("Specify an exact NuGet version, not a range or MSBuild expression.");
        var changes = new List<string>();
        foreach (var package in Packages.Where(p => p.Version != version))
        {
            var owner = documents.Single(d => d.Value == package.VersionNode!.Document).Key;
            changes.Add($"{package.Name}: {package.Version} -> {version} ({owner})");
            if (package.VersionNode is XAttribute a) a.Value = version;
            else ((XElement)package.VersionNode!).Value = version;
        }
        return changes;
    }

    public string FeaturePackage(string feature) => feature.ToLowerInvariant() switch
    {
        "widgets" => "Gondwana.Widgets",
        "audio" when Host == "Blazor" => "Gondwana.Audio.Browser",
        "audio" => "Gondwana", // Desktop NAudio support lives in core on master.
        "midi" when Host != "Blazor" => "Gondwana.Audio.Midi",
        "gamepad" when Host != "Blazor" => "Gondwana.Input.SDL2",
        "video" when Host != "Blazor" => "Gondwana.Video",
        "hosting" when Host is not null => $"Gondwana.{Host}.Hosting",
        "hosting" => throw new InvalidOperationException("Cannot select hosting without one unambiguous WinForms, Avalonia, or Blazor adapter."),
        "midi" or "gamepad" or "video" => throw new InvalidOperationException($"'{feature}' uses native desktop dependencies and cannot be added to a Blazor game."),
        _ => throw new InvalidOperationException($"Unknown feature '{feature}'. Available: widgets, audio, midi, gamepad, video, hosting.")
    };

    public IReadOnlyList<string> Add(string feature)
    {
        var name = FeaturePackage(feature);
        if (Packages.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) || References.Contains(name, StringComparer.OrdinalIgnoreCase)) return [];
        var version = AlignedVersion();
        var reference = new XElement(Project.Root!.Name.Namespace + "PackageReference", new XAttribute("Include", name));
        if (CentrallyManaged)
        {
            if (Central is null) throw new InvalidOperationException("No editable Directory.Packages.props found.");
            var entries = Central.Descendants().Where(e => e.Name.LocalName == "PackageVersion" && Name(e).Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length > 0 && (entries.Length != 1 || Conditional(entries[0]) || Value(ResolveProperty(VersionNode(entries[0], "Version"))) != version))
                throw new InvalidOperationException($"Central version for {name} is ambiguous or differs from {version}; align it manually first.");
            if (entries.Length == 0) AppendItem(Central, new XElement(Central.Root!.Name.Namespace + "PackageVersion", new XAttribute("Include", name), new XAttribute("Version", version)));
        }
        else reference.Add(new XAttribute("Version", version));
        AppendItem(Project, reference);
        return [$"Add {name} {version} to {Path}" + (CentrallyManaged ? " (centrally managed; Directory.Packages.props may also change)" : "")];
    }

    private static void AppendItem(XDocument doc, XElement item)
    {
        var root = doc.Root!;
        var group = root.Elements().LastOrDefault(e => e.Name.LocalName == "ItemGroup" && !Conditional(e) && e.Elements(item.Name).Any());
        var newline = doc.ToString(SaveOptions.DisableFormatting).Contains("\r\n") ? "\r\n" : "\n";
        if (group is null)
        {
            group = new XElement(root.Name.Namespace + "ItemGroup", new XText(newline + "    "));
            root.Add(new XText(newline + "    "), group, new XText(newline));
        }
        var indent = group.Elements().LastOrDefault()?.PreviousNode as XText;
        var whitespace = indent?.Value ?? newline + "        ";
        if (group.LastNode is XText last && string.IsNullOrWhiteSpace(last.Value)) last.AddBeforeSelf(new XText(whitespace), item);
        else group.Add(new XText(whitespace), item, new XText(newline + "    "));
    }

    public void Save()
    {
        // Validate all owners before writing any of them; never overwrite concurrent edits.
        foreach (var (path, bytes) in originals)
            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException($"{path} changed during inspection. Retry the command.");
        foreach (var (path, document) in documents)
        {
            var oldDoc = XDocument.Load(new MemoryStream(originals[path]), LoadOptions.PreserveWhitespace);
            if (XNode.DeepEquals(oldDoc, document)) continue;
            var bytes = originals[path];
            var encoding = bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe ? Encoding.Unicode
                : bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff ? Encoding.BigEndianUnicode
                : new UTF8Encoding(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
            var temporary = path + ".gondwana-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var newline = encoding.GetString(bytes).Contains("\r\n") ? "\r\n" : "\n";
                using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Encoding = encoding, Indent = false, OmitXmlDeclaration = document.Declaration is null, NewLineHandling = NewLineHandling.Replace, NewLineChars = newline })) document.Save(writer);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
