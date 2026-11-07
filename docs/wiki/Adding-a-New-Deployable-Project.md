When a new `.csproj` should become a first-class Gondwana package and ship through the normal build/release pipeline, use this checklist.

The goal is not merely to make the project compile. A deployable project should be discoverable, packable, included in the appropriate changelogs and release automation, and covered by API/documentation tooling where applicable.

## Required manual setup

Create the project with appropriate platform dependencies, add solution membership,
NuGet metadata and packaged README/CHANGELOG/icon, register changelog behavior,
update package inventories and CLI mappings where applicable, and add explicit
CI/release binary staging when needed. Verify labeling and release-category
coverage; reuse an existing family wildcard rather than creating redundant rules.

## Usually automatic, but must be verified

Solution-wide NuGet packing/publishing, recursive API documentation discovery,
and shared NBGV/version metadata normally need no project-specific registry or
publish command. Verify their current configuration and inspect the produced
package, including its dependency target frameworks and packed documentation.

---

## ✅ Required — add the project to the solution

Add the project to `Gondwana.sln`:

```sh
dotnet sln Gondwana.sln add <ProjectPath>/<ProjectName>.csproj
```

The repository CI and release workflows build and pack the solution. Keeping deployable projects in the solution ensures they participate in ordinary restore, build, pack, and dependency validation.

After adding the project, verify a normal solution build with Windows targeting enabled when the solution contains Windows projects:

```sh
dotnet restore /p:EnableWindowsTargeting=true
dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true
```

---

## ✅ Required — project file / NuGet package metadata

### `<ProjectName>/<ProjectName>.csproj`

Confirm the project has package metadata appropriate to its target framework and purpose.

A typical library project includes:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <OutputType>Library</OutputType>
  <RootNamespace>Your.Project.Name</RootNamespace>
  <AssemblyName>Your.Project.Name</AssemblyName>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>

  <!-- URLs / repository -->
  <PackageProjectUrl>https://github.com/Isthimius/Gondwana/tree/master/Your.Project.Name</PackageProjectUrl>

  <!-- NuGet packaging -->
  <IsPackable>true</IsPackable>
  <PackageId>Your.Project.Name</PackageId>
  <Title>Your Project Name</Title>
  <Description>Short package-specific description for NuGet.</Description>
  <PackageTags>gondwana gamedev csharp dotnet relevant-project-tags</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>gondwana-logo.png</PackageIcon>
  <PackageReleaseNotes>Full changelog: https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md</PackageReleaseNotes>
</PropertyGroup>
```

Use the real target framework for the project. Windows-only libraries commonly use `net8.0-windows`; browser projects may use `net8.0-browser`. Do not copy `net8.0` blindly from the example.

> **Package description rule**  
> Keep `<Description>` specific to the package. Do not reuse the core engine description unless the package actually contains the core engine.

> **Package tags rule**  
> Include general Gondwana tags plus package-specific tags. Platform adapters should include their platform, tooling packages should include tooling/template/CLI tags, and subsystem packages should include the relevant subsystem terms.

---

## ✅ Required — package README, CHANGELOG, and icon

A deployable package should have a `README.md` and `CHANGELOG.md` at the project root and include them in its NuGet package.

If the package uses the shared Gondwana icon, include that as well:

```xml
<ItemGroup>
  <None Include="README.md" Pack="true" PackagePath="" />
  <None Include="CHANGELOG.md" Pack="true" PackagePath="" />
  <None Include="..\gondwana-logo.png" Pack="true" PackagePath="" Link="gondwana-logo.png" />
</ItemGroup>
```

The package README should explain installation, purpose, important setup, and related Gondwana packages. Link its release history to that project's own `CHANGELOG.md`.

---

## ✅ Required — changelog configuration

### `Tooling/scripts/Changelog-ProjectGroups.ps1`

This file is the **authoritative project metadata** for Gondwana changelog generation. Do not add new project lists to `release.ps1` or `Generate-Project-Changelogs.ps1`; those older per-script lists have been replaced by this shared configuration.

Add the new project to `$ChangelogProjects` with the intended behavior:

```powershell
[pscustomobject]@{
    Path = "Your.Project.Name"
    RootName = "Your.Project.Name"
    GenerateChangelog = $true
    IncludeInRootChangelog = $true
}
```

For ordinary deployable packages:

- `GenerateChangelog = $true` creates/updates `<Project>/CHANGELOG.md`.
- `IncludeInRootChangelog = $true` also includes the project's commits in the repository-level `CHANGELOG.md`.

A project may intentionally choose different values. Demos, for example, normally generate their own changelogs but are excluded from the root changelog.

After editing the project groups, validate the configuration:

```powershell
./Tooling/scripts/Test-ChangelogConfiguration.ps1
```

The changelog generators derive project include globs from `Path`; a normal project should not need a second project-path list elsewhere.

---

## ✅ Required — CI integration-test coverage

### `.github/ci-paths.yml` and `.github/workflows/ci-master.yml`

The normal PR/master build covers all projects added to the solution, but expensive integration suites are **selected by changed paths**. When adding or changing a project:

1. Check whether its code, package inputs, and test fixtures are included in the `blazor`, `winforms`, or `winforms_shared` filters when those integration suites depend on it.
2. For a new Windows test suite, add its paths to the appropriate `winforms_*` filter(s), register its project in `Testing/WinFormsEditorTests.slnf`, and add it to the selector/map and test steps in `ci-master.yml`.
3. Update Studio dependency selection when an editor, importer, or plugin becomes part of Studio.
4. Confirm a PR changing **only that project's files** selects the intended suites; consider fixtures and transitive project references as well.

Nightly, manual, and tagged-release CI runs execute the **full** integration suite, regardless of which files changed. The tagged-release workflow requires a successful full run before packaging and publication.

---

## ✅ Required — downloadable and release binary staging

### `.github/workflows/package-binaries.yml`

The CI workflow no longer stages the full Windows binary ZIP. The **reusable packaging workflow** is the single place for NuGet package creation and release binary staging. It runs daily, can be dispatched manually, and is invoked by tagged releases **after full CI validation**.

If a new deployable project must appear in `Gondwana-binaries.zip`:

1. Add its publish destination and `dotnet publish` command to **Publish projects** in `package-binaries.yml`.
2. Add the matching DLL or application folder to **Stage release assets**.
3. For tooling applications with runtime assets, add the project to the shared `Tooling/scripts/Publish-WindowsTooling.ps1` plan as appropriate.
4. Update `Tooling/scripts/Test-WindowsToolingBundle.ps1` if the new binary is part of the required bundle contract.

Typical library example in the packaging workflow:

```bash
mkdir -p publish/Gondwana.NewProject
dotnet publish Gondwana.NewProject/Gondwana.NewProject.csproj \
  -c Release -o publish/Gondwana.NewProject \
  --nologo /p:EnableWindowsTargeting=true
```

and in the release staging step:

```bash
cp publish/Gondwana.NewProject/Gondwana.NewProject.dll release-assets/
```

The tagged `.github/workflows/release.yml` calls the same packaging workflow; **do not duplicate publish/staging commands there**. Packable solution projects normally participate in its `dotnet pack` step and the resulting `nupkgs/*.nupkg` files are published without adding package-specific NuGet push commands.

---

## ✅ Required — PR labels and generated GitHub release notes

### `.github/labeler.yml`

Verify that changes under the new project receive the appropriate PR label.

Before adding another rule, check whether an existing family rule already covers the project. For example, the audio family rule matches future `Gondwana.Audio*` projects automatically.

If no suitable rule exists, add one using the repository's CHANGELOG exclusion pattern:

```yml
gondwana-newproject:
  - changed-files:
    - all-globs-to-any-file:
      - 'Gondwana.NewProject/**'
      - '!**/CHANGELOG.md'
```

For a companion package, multiple paths may share one project-family label when that grouping is intentional.

### `.github/release.yml`

Verify that the resulting label is matched by the correct generated-release-notes category.

Again, reuse an existing family category where appropriate rather than creating a redundant category. If a new category is needed:

```yml
    - title: Gondwana.NewProject
      labels:
        - gondwana-newproject
        - Gondwana.NewProject
```

Remember that release-note categories are matched in order. Broad change-type categories such as Breaking Changes, New Features, and Fixes currently appear before package categories.

---

## ✅ Required — repository/package inventory documentation

Add the package anywhere the repository presents the deployable package family, as appropriate. At minimum, review:

- root `README.md` package table
- the core/package `README.md` **Related Packages** sections
- `docs/ai/repository-map.md` when the project adds a new top-level repository area
- tooling/template documentation if the new package changes scaffolding or CLI behavior

Do not mechanically add every package to every document; update inventories whose purpose is to describe the available package set or repository layout.

---

## ✅ Verify — API documentation coverage

Gondwana's Doxygen API publisher scans the repository source tree recursively. A normal top-level Gondwana library does **not** need to be added to a separate API-doc project registry.

Verify two things:

1. The project's source is not excluded by `EXCLUDE_PATTERNS` in `docs/doxy/Gondwana_doxy`.
2. `.github/workflows/docs.yml` will trigger when that project's `.cs` or `.csproj` files change.

Top-level runtime packages such as `Gondwana.Audio.*` are currently included automatically. Tests, demos, tooling, games, and other explicitly excluded trees intentionally do not appear in the public engine API reference.

If a new source tree should be included but falls under an existing exclusion or a new file type/language is introduced, update both the Doxygen configuration and the docs workflow trigger rules.

---

## ✅ Verify — shared build/version metadata

Normal Gondwana projects inherit common repository build/version settings from the root configuration files. Review:

- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props` when central package versions are in use
- `version.json` / NBGV behavior

Usually no edit is required. The purpose of this step is to avoid adding package-specific versioning or build behavior that fights the repository-wide conventions.

---

## ✅ Final validation

Before considering the project deployable, verify at least:

```sh
dotnet restore /p:EnableWindowsTargeting=true
dotnet build -c Release --no-restore /p:EnableWindowsTargeting=true
dotnet pack -c Release --no-build /p:EnableWindowsTargeting=true
```

and run the repository test suite plus:

```powershell
./Tooling/scripts/Test-ChangelogConfiguration.ps1
```

For API-bearing runtime packages, also confirm the development API workflow can see the project or run the local API-doc build described under `Tooling/scripts/api-docs/README.md`.

A new deployable project is ready when it is in the solution, builds and packs with the repository, has package metadata and documentation, participates in the intended changelog/release paths, and is covered by the appropriate repository automation.
