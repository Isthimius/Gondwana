# Adding a New Deployable Project

When a new `.csproj` is added to `Gondwana.sln` for NuGet deployment, update the following files.

## Required — CI pipeline

### `.github/workflows/ci-master.yml`

1. **"Publish projects needed for staged binaries"** step — add a `mkdir -p publish/<ProjectName>` line and a corresponding `dotnet publish` line.
2. **"Stage exact downloadable binaries"** step — add a `cp publish/<ProjectName>/<ProjectName>.dll artifacts/` line (or `cp -r` / `.exe` for apps/tools).

### `.github/workflows/release.yml`

1. **"Publish projects"** step — same pattern: `mkdir -p publish/<ProjectName>` and `dotnet publish`.
2. **"Stage release assets"** step — add the matching `cp` line into `release-assets/`.

> **Note for apps/tools** (e.g. WinForms executables): use `-r win-x64 --self-contained false` in `dotnet publish` and `cp -r` (full folder) when staging, to include runtime assets.

---

## Recommended — changelog grouping

### `Tooling/scripts/release.ps1` — `$ProjectChangelogGroups` list

Add a new entry:

```powershell
[pscustomobject]@{
    Name         = "Your.Project.Name"
    IncludePaths = @("Your/Project/Path/**/*")
}
```

### `Tooling/scripts/Generate-Project-Changelogs.ps1` — `$DefaultProjects` list

Add the repo-root-relative folder path, e.g. `"Gondwana.NewProject"` or `"Tooling/Gondwana.NewProject"`.

---

## Not required

`Deploy-Gondwana-Itch.ps1` and `Deploy-Gondwana-Website.ps1` are project-parameter driven and do not need updating.
