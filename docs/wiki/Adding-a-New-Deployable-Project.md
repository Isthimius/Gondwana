When a new `.csproj` is added to `Gondwana.sln` and should be included in NuGet deployments, update the following files.

## ✅ Required — project file / NuGet package metadata

### `<ProjectName>/<ProjectName>.csproj`

Confirm the project has the required NuGet packaging metadata in its `.csproj` file.

At minimum, deployable package projects should include:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <OutputType>Library</OutputType>
  <RootNamespace>Your.Project.Name</RootNamespace>
  <AssemblyName>Your.Project.Name</AssemblyName>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>

  <!-- URLs / repository -->
  <PackageProjectUrl>https://github.com/Isthimius/Gondwana</PackageProjectUrl>

  <!-- NuGet packaging -->
  <IsPackable>true</IsPackable>
  <PackageId>Your.Project.Name</PackageId>
  <Title>Your Project Name</Title>
  <Description>Short package-specific description for NuGet.</Description>
  <PackageTags>gondwana gamedev csharp dotnet 2d 2.5d skia skiasharp relevant-project-tags</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>gondwana-logo.png</PackageIcon>
  <PackageReleaseNotes>Full changelog: https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md</PackageReleaseNotes>
</PropertyGroup>
```

Confirm the project has a package README.md at the csproj root and is referenced in the package. If the package uses the shared Gondwana icon, include it in the package as well. Combined, your csproj should include something similar to this:

```xml
<ItemGroup>
	<None Include="README.md" Pack="true" PackagePath="" />
	<None Include="CHANGELOG.md" Pack="true" PackagePath="" />
	<None Include="..\gondwana-logo.png" Link="gondwana-logo.png">
		<PackagePath></PackagePath>
		<Pack>true</Pack>
	</None>
</ItemGroup>
```

> **Package description rule**  
> Keep `<Description>` specific to the package. Do not reuse the core engine description unless the package actually contains the core engine.

> **Package tags rule**  
> Include general Gondwana tags plus package-specific tags. For example, platform adapters should include their platform name, tooling projects should include tooling/template/CLI tags, and widget/UI packages should include UI/HUD/menu/dialog-related tags.

---

## ✅ Required — CI pipeline

### `.github/workflows/ci-master.yml`

1. **"Publish projects needed for staged binaries"** step — add a `mkdir -p publish/<ProjectName>` line and a corresponding `dotnet publish` line.
2. **"Stage exact downloadable binaries"** step — add a `cp publish/<ProjectName>/<ProjectName>.dll artifacts/` line, or use `cp -r` / `.exe` for apps and tools.

### `.github/workflows/release.yml`

1. **"Publish projects"** step — same pattern: add `mkdir -p publish/<ProjectName>` and `dotnet publish`.
2. **"Stage release assets"** step — add the matching `cp` line into `release-assets/`.

> **Note for apps/tools**  
> For apps/tools such as WinForms executables, use `-r win-x64 --self-contained false` in `dotnet publish` and `cp -r` when staging, so runtime assets are included.

---

## ✅ Required — PR labels and generated release notes

### `.github/labeler.yml`

Add a label rule so pull requests touching the new project are automatically labeled.

Example:

```yml
gondwana-newproject:
  - changed-files:
    - any-glob-to-any-file: 'Gondwana.NewProject/**'
```

For projects under `Tooling/`, use the tooling path:

```yml
gondwana-newproject:
  - changed-files:
    - any-glob-to-any-file: 'Tooling/Gondwana.NewProject/**'
```

If the project has a companion hosting package, include both paths under the same label:

```yml
gondwana-newproject:
  - changed-files:
    - any-glob-to-any-file:
      - 'Gondwana.NewProject/**'
      - 'Gondwana.NewProject.Hosting/**'
```

### `.github/release.yml`

Add a changelog category so generated GitHub release notes group pull requests for the new project.

Example:

```yml
    - title: Gondwana.NewProject
      labels:
        - gondwana-newproject
        - Gondwana.NewProject
```

Place the new category near related packages.

For example, a core-adjacent package should go near `Gondwana (Core)`, while a platform adapter should go near the other adapter packages.

> **Ordering note**  
> Categories are matched in order. If a pull request has both `enhancement` and `gondwana-newproject`, it will appear under whichever matching category comes first. Keep the broad change-type categories first if you want release notes grouped by change type; place project categories first if you want them grouped by package.

---

## ✅ Recommended — changelog grouping

### `Tooling/scripts/release.ps1` — `$ProjectChangelogGroups` list

Add a new entry:

```powershell
[pscustomobject]@{
    Name         = "Your.Project.Name"
    IncludePaths = @("Your/Project/Path/**/*")
}
```

Example:

```powershell
[pscustomobject]@{
    Name         = "Gondwana.NewProject"
    IncludePaths = @("Gondwana.NewProject/**/*")
}
```

### `Tooling/scripts/Generate-Project-Changelogs.ps1` — `$DefaultProjects` list

Add the repo-root-relative folder path.

Examples:

```powershell
"Gondwana.NewProject"
```

or:

```powershell
"Tooling/Gondwana.NewProject"
```

---

## ℹ️ Example — Gondwana.Widgets

For `Gondwana.Widgets`, the expected additions would be:

### `Gondwana.Widgets/Gondwana.Widgets.csproj`

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <OutputType>Library</OutputType>
  <RootNamespace>Gondwana.Widgets</RootNamespace>
  <AssemblyName>Gondwana.Widgets</AssemblyName>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>

  <!-- URLs / repository -->
  <PackageProjectUrl>https://github.com/Isthimius/Gondwana</PackageProjectUrl>

  <!-- NuGet packaging -->
  <IsPackable>true</IsPackable>
  <PackageId>Gondwana.Widgets</PackageId>
  <Title>Gondwana Widgets</Title>
  <Description>Reusable UI and gameplay widgets for the Gondwana game engine, including menus, dialogs, labels, buttons, overlays, HUD elements, and DirectDrawing-friendly 2D/2.5D game controls.</Description>
  <PackageTags>gondwana widgets ui game-ui gamedev csharp dotnet 2d 2.5d directdrawing hud menus dialogs buttons overlays skia skiasharp</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>gondwana-logo.png</PackageIcon>
  <PackageReleaseNotes>Full changelog: https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md</PackageReleaseNotes>
</PropertyGroup>

<ItemGroup>
  <None Include="README.md" Pack="true" PackagePath="" />
  <None Include="CHANGELOG.md" Pack="true" PackagePath="" />
  <None Include="..\gondwana-logo.png" Link="gondwana-logo.png">
    <PackagePath></PackagePath>
    <Pack>true</Pack>
  </None>
</ItemGroup>
```

### `.github/workflows/ci-master.yml`

In the **"Publish projects needed for staged binaries"** step, add:

```bash
mkdir -p publish/Gondwana.Widgets
dotnet publish Gondwana.Widgets/Gondwana.Widgets.csproj -c Release -o publish/Gondwana.Widgets
```

In the **"Stage exact downloadable binaries"** step, add:

```bash
cp publish/Gondwana.Widgets/Gondwana.Widgets.dll artifacts/
```

### `.github/workflows/release.yml`

In the **"Publish projects"** step, add:

```bash
mkdir -p publish/Gondwana.Widgets
dotnet publish Gondwana.Widgets/Gondwana.Widgets.csproj -c Release -o publish/Gondwana.Widgets
```

In the **"Stage release assets"** step, add:

```bash
cp publish/Gondwana.Widgets/Gondwana.Widgets.dll release-assets/
```

### `.github/labeler.yml`

```yml
gondwana-widgets:
  - changed-files:
    - any-glob-to-any-file: 'Gondwana.Widgets/**'
```

### `.github/release.yml`

```yml
    - title: Gondwana.Widgets
      labels:
        - gondwana-widgets
        - Gondwana.Widgets
```

### `Tooling/scripts/release.ps1`

```powershell
[pscustomobject]@{
    Name         = "Gondwana.Widgets"
    IncludePaths = @("Gondwana.Widgets/**/*")
}
```

### `Tooling/scripts/Generate-Project-Changelogs.ps1`

```powershell
"Gondwana.Widgets"
```
