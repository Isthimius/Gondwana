param(
    [string] $WorkRoot = (Join-Path ([IO.Path]::GetTempPath()) ('gondwana-consumer-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
if ($WorkRoot.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a work directory outside the repository (isolated SDK, NuGet and template state).'
}
if ((Test-Path $WorkRoot) -and @(Get-ChildItem -Force $WorkRoot).Count -gt 0) {
    throw 'WorkRoot must be empty to prevent cached consumer or package results.'
}
New-Item -ItemType Directory -Force $WorkRoot | Out-Null
$feed = New-Item -ItemType Directory -Force "$WorkRoot/feed"
function Run([string] $Command, [string[]] $Arguments) {
    Write-Host "$Command $($Arguments -join ' ')"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE)" }
}
$previousPackages = $env:NUGET_PACKAGES
$previousCliHome = $env:DOTNET_CLI_HOME
$previousIntegrityCheck = $env:DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK
try {
    # Do not migrate unrelated VS/mobile workloads when initializing the isolated CLI home.
    # Compatibility is explicitly checked below, and native build/publish must still succeed.
    $env:DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK = '1'
    Push-Location $repo
    try {
        # Keep the runtime libraries on the repository's SDK 8 build path.
        foreach ($project in @('Gondwana', 'Gondwana.Widgets', 'Gondwana.Hosting', 'Gondwana.Blazor', 'Gondwana.Blazor.Hosting', 'Gondwana.Audio.Browser', 'Tooling/Gondwana.Templates', 'Tooling/Gondwana.Cli')) {
            $name = Split-Path $project -Leaf
            Run dotnet @('pack', "$project/$name.csproj", '-c', 'Release', '-o', $feed.FullName, '--nologo')
        }
    } finally { Pop-Location }

    # Use NBGV's actual package version; never assume command-line Version overrides it.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $templatePackages = @(Get-ChildItem "$feed/Gondwana.Templates.*.nupkg")
    if ($templatePackages.Count -ne 1) { throw 'Use a clean work directory with exactly one template package' }
    $zip = [IO.Compression.ZipFile]::OpenRead($templatePackages[0].FullName)
    try {
        $reader = [IO.StreamReader]::new(($zip.Entries | Where-Object FullName -like '*.nuspec').Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $version = $nuspec.package.metadata.version
        $template = $zip.GetEntry('content/templates/gondwana-blazor/MyGame.csproj')
        if (!$template) { throw 'Distributed template project missing' }
    } finally { $zip.Dispose() }
    $zip = [IO.Compression.ZipFile]::OpenRead("$feed/Gondwana.Audio.Browser.$version.nupkg")
    try {
        if (!$zip.GetEntry('staticwebassets/gondwana-audio.js')) { throw 'Packaged static web asset missing' }
        $reader = [IO.StreamReader]::new(($zip.Entries | Where-Object FullName -like '*.nuspec').Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($nuspec.package.metadata.contentFiles.files | Where-Object { $_.include -like '*gondwana-audio.js' -and $_.buildAction -eq 'Compile' }) {
            throw 'Audio must not be packaged as C# contentFiles'
        }
        $nuspec.Save("$WorkRoot/audio.nuspec")
    } finally { $zip.Dispose() }

    # An independent global.json prevents the repository SDK 8 pin from leaking in.
    '{"sdk":{"version":"10.0.100","rollForward":"latestFeature","allowPrerelease":false}}' |
        Set-Content "$WorkRoot/global.json"
    $env:NUGET_PACKAGES = "$WorkRoot/packages"
    $env:DOTNET_CLI_HOME = "$WorkRoot/cli-home"
    Push-Location $WorkRoot
    try {
        $sdk = & dotnet --version
        if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.0\.') { throw "Expected SDK 10.0, got $sdk" }
        $workloads = & dotnet workload list
        $workloads | Set-Content toolchain.txt
        if ($LASTEXITCODE -ne 0 -or ($workloads -join "`n") -notmatch '(?m)^wasm-tools\s+10\.0\.') {
            throw 'Install wasm-tools with the selected .NET 10 SDK before running this test.'
        }
        "SDK: $sdk; target: net10.0-browser" | Add-Content toolchain.txt
        $escapedFeed = [System.Security.SecurityElement]::Escape($feed.FullName)
        @"
<configuration>
  <packageSources><clear/><add key="local" value="$escapedFeed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Gondwana*"/></packageSource>
    <packageSource key="nuget.org"><package pattern="*"/></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content NuGet.Config
        Run dotnet @('new', 'install', "$feed/Gondwana.Templates.$version.nupkg")
        Run dotnet @('tool', 'install', 'Gondwana.Cli', '--version', $version, '--tool-path', "$WorkRoot/tools", '--configfile', 'NuGet.Config')
        $cli = Join-Path "$WorkRoot/tools" $(if ($IsWindows) { 'gondwana.exe' } else { 'gondwana' })
        Run $cli @('new', 'blazor', 'Consumer', '-o', 'app')
        [xml] $projectXml = Get-Content app/Consumer.csproj
        if ($projectXml.Project.PropertyGroup.TargetFramework -ne 'net10.0-browser') { throw 'Wrong generated target framework' }
        foreach ($reference in $projectXml.Project.ItemGroup.PackageReference) {
            if ($reference.Include -like 'Gondwana*') { $reference.Version = "[$version]" }
        }
        $projectXml.Save("$WorkRoot/app/Consumer.csproj")
        if (Test-Path app/wwwroot/gondwana-audio.js) { throw 'Audio must come from the package, not template source' }
        Run dotnet @('restore', 'app/Consumer.csproj', '--configfile', 'NuGet.Config', '-p:Configuration=Release')
        $nativeItems = & dotnet msbuild app/Consumer.csproj -getItem:NativeFileReference -getProperty:WasmBuildNative
        if ($LASTEXITCODE -ne 0 -or ($nativeItems -join "`n") -notmatch 'libSkiaSharp\.a') {
            throw 'SkiaSharp native WASM link inputs missing for the selected SDK/target'
        }
        $nativeItems | Set-Content native-items.json
        Run dotnet @('build', 'app/Consumer.csproj', '-c', 'Release', '--no-restore', '-bl:build.binlog')
        Run dotnet @('publish', 'app/Consumer.csproj', '-c', 'Release', '--no-restore', '-o', 'published', '-bl:publish.binlog')
        # Match a new user's default dotnet run configuration as well as Release publishing.
        Run dotnet @('build', 'app/Consumer.csproj', '-c', 'Debug', '--no-restore', '-bl:debug-build.binlog')
        $compileItems = & dotnet msbuild app/Consumer.csproj -t:ResolveReferences -getItem:Compile -p:Configuration=Release
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect compilation items' }
        $compileItems | Set-Content compile-items.json
        if (($compileItems -join "`n") -match 'gondwana-audio\.js') { throw 'JavaScript included as C# compilation input' }
        if (!(Test-Path published/wwwroot/gondwana-audio.js)) { throw 'Published audio module missing' }
        if (Test-Path app/wwwroot/gondwana-audio.js) { throw 'Build unexpectedly copied audio into source wwwroot' }
        Copy-Item "$PSScriptRoot/package.json", "$PSScriptRoot/package-lock.json", "$PSScriptRoot/browser.mjs" $WorkRoot
        Run npm @('ci', '--no-audit', '--no-fund')
        Run npx @('playwright', 'install', 'chromium')
        Run node @('browser.mjs', $WorkRoot, $version)
    } finally { Pop-Location }
} finally {
    $env:NUGET_PACKAGES = $previousPackages
    $env:DOTNET_CLI_HOME = $previousCliHome
    $env:DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK = $previousIntegrityCheck
    Write-Host "Consumer test artifacts: $WorkRoot"
}
