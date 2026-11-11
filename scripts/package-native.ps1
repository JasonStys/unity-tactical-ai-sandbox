#requires -Version 7.4
# File: package-native.ps1
# Purpose: Build the genuine self-contained Windows designer, preserve actual runtime notices, archive/extract and exercise the extracted EXE twice.
# Variables: fresh output tree, checked exact source/runtime and synthetic acceptance root; no installation or default user profile.
# Author: Jason Stys. No release, tag, external Unity Editor or system SDK modification.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputRoot, [string]$Dotnet = 'dotnet', [switch]$AllowDirty)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$output = [IO.Path]::GetFullPath($OutputRoot)
if (-not [IO.Path]::IsPathFullyQualified($OutputRoot) -or (Test-Path -LiteralPath $output)) { throw 'Choose a new absolute output directory.' }
if (-not $IsWindows) { throw 'This genuine WinForms package requires Windows x64.' }
if ($env:GITHUB_ACTIONS -eq 'true' -and $AllowDirty) { throw 'CI cannot label dirty sources as accepted.' }
$source = (& git -c "safe.directory=$($root.Replace('\','/'))" -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $source -notmatch '^[a-f0-9]{40}$') { throw 'Exact Git source unavailable.' }
$changed = & git -c "safe.directory=$($root.Replace('\','/'))" -C $root status --porcelain --untracked-files=all
if ($LASTEXITCODE -ne 0 -or ($changed -and -not $AllowDirty)) { throw 'Clean source required; explicitly label local candidates.' }
if ($env:GITHUB_ACTIONS -eq 'true' -and $env:GITHUB_SHA -ne $source) { throw 'CI checkout identity mismatch.' }
$version = '1.1.0'
$sdk = (& $Dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdk -ne '10.0.404') { throw 'The current isolated or hosted .NET10.0.404 SDK is required.' }
$project = Join-Path $root 'src/TacticalAI.Designer/TacticalAI.Designer.csproj'
$projectXml = [xml][IO.File]::ReadAllText($project, $utf8)
if ($projectXml.Project.PropertyGroup.Version -ne $version -or $projectXml.Project.PropertyGroup.RuntimeFrameworkVersion -ne '10.0.15') { throw 'Native product/runtime version mismatch.' }
New-Item -ItemType Directory -Path $output | Out-Null
$payload = Join-Path $output 'UnityTacticalAiDesigner'
$extracted = Join-Path $output 'extracted'
& $Dotnet restore $project --locked-mode --configfile (Join-Path $PSScriptRoot 'NuGet.Native.Config') *> (Join-Path $output 'restore.log')
if ($LASTEXITCODE -ne 0) { throw 'Signed locked native restore failed; preserve the log.' }
& $Dotnet publish $project -c Release --no-restore --output $payload -p:DebugType=None -p:DebugSymbols=false *> (Join-Path $output 'publish.log')
if ($LASTEXITCODE -ne 0) { throw 'Real self-contained publication failed; preserve the log.' }
$runtime = Get-Content -LiteralPath (Join-Path $payload 'TacticalAiDesigner.runtimeconfig.json') -Raw | ConvertFrom-Json
$frameworks = @($runtime.runtimeOptions.includedFrameworks)
foreach ($name in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
    $actual = @($frameworks | Where-Object name -eq $name)
    if ($actual.Count -ne 1 -or $actual[0].version -ne '10.0.15') { throw "Actual included runtime mismatch: $name" }
}
foreach ($name in @('coreclr.dll','hostfxr.dll','System.Private.CoreLib.dll','System.Windows.Forms.dll','TacticalAiDesigner.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $name) -PathType Leaf)) { throw "Required self-contained native payload missing: $name" }
}
$runtimeVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'System.Private.CoreLib.dll')).ProductVersion
if ($runtimeVersion -notmatch '^10\.0\.15(?:-servicing\.[0-9.]+)?(?:\+[a-f0-9]{40})?$') { throw 'Actual runtime DLL is not the required patched version.' }
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
foreach ($pair in @(@('System.Private.CoreLib.dll','microsoft.netcore.app.runtime.win-x64'),@('System.Windows.Forms.dll','microsoft.windowsdesktop.app.runtime.win-x64'))) {
    $vendor = Join-Path $packages ($pair[1]+'/10.0.15/runtimes/win-x64/lib/net10.0/'+$pair[0])
    if ((Get-FileHash -LiteralPath (Join-Path $payload $pair[0])).Hash -ne (Get-FileHash -LiteralPath $vendor).Hash) { throw 'Published runtime bytes differ from the exact restored vendor package.' }
}
$notices = Join-Path $payload 'notices'
New-Item -ItemType Directory -Path $notices | Out-Null
$noticeFiles = @(
    @('microsoft.netcore.app.runtime.win-x64/10.0.15/LICENSE.TXT','dotnet-LICENSE.txt'),
    @('microsoft.netcore.app.runtime.win-x64/10.0.15/THIRD-PARTY-NOTICES.TXT','dotnet-THIRD-PARTY-NOTICES.txt'),
    @('microsoft.windowsdesktop.app.runtime.win-x64/10.0.15/LICENSE','windowsdesktop-LICENSE.txt')
)
$noticeProof = @()
foreach ($pair in $noticeFiles) {
    $inputPath = Join-Path $packages $pair[0]
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Required actual vendor notice missing: $($pair[0])" }
    $target = Join-Path $notices $pair[1]
    Copy-Item -LiteralPath $inputPath -Destination $target
    $noticeProof += @{path=('notices/'+$pair[1]);sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()}
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $payload 'LICENSE.txt')
[IO.File]::WriteAllText((Join-Path $payload 'START-HERE.txt'), "Unity Tactical AI Designer $version by Jason Stys`n`nExtract this COMPLETE directory, then open TacticalAiDesigner.exe. No installed .NET, Node, Python or Unity Editor is required. Windows x64 only; unsigned local portable application. OS security warnings are not bypassed.`n`nActual game authoring, legal turns, replay inspection and seeded balance run offline. Prepared encounters are editable data inside the full application. Durable plaintext profiles live outside this directory in LocalAppData/JasonStysPortfolio/unity-tactical-ai-sandbox/workspace. Keep profiles/exports private. Help is inside the visible application. No Unity PlayMode, real-world control, account authentication or signed installer claim. github.com/JasonStys`n", $utf8)
$entries = @()
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A release member is an alias.' }
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName).Replace('\','/')
    $entries += @{path=$relative;bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$metadata = @{schema=1;product='unity-tactical-ai-sandbox';version=$version;sourceCommit=$source;sourceDirty=[bool]$changed;platform='windows-x64';sdk=$sdk;runtime='10.0.15';actualRuntimeDllVersion=$runtimeVersion;files=$entries;notices=$noticeProof}
[IO.File]::WriteAllText((Join-Path $payload 'RELEASE.json'), ($metadata | ConvertTo-Json -Depth 8)+"`n", $utf8)
$archive = Join-Path $output "UnityTacticalAiDesigner-$version-windows-x64.zip"
Compress-Archive -LiteralPath $payload -DestinationPath $archive -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive+'.sha256'), ($archiveHash+'  '+[IO.Path]::GetFileName($archive)+"`n"), $utf8)
# Inspect and stream the actual created ZIP into a new directory with finite real byte budgets.
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -lt 20 -or $zip.Entries.Count -gt 2048) { throw 'Unexpected native archive member count.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $total = [long]0
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName
        if ($name.Contains('\') -or $name.StartsWith('/') -or $name.Contains(':') -or ($name.Split('/') -contains '..') -or -not $seen.Add($name) -or (($entry.ExternalAttributes -shr 16) -band 61440) -eq 40960) { throw 'Unsafe native archive member.' }
        if ($entry.Length -gt 268435456) { throw 'Native archive member exceeds its bound.' }
        if ($name.EndsWith('/')) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $extracted $name))
        if (-not $target.StartsWith($extracted+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escaped extraction root.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        $inputStream = $entry.Open()
        $outputStream = [IO.File]::Open($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try {
            $buffer = [byte[]]::new(65536); $count = [long]0
            while (($read = $inputStream.Read($buffer,0,$buffer.Length)) -gt 0) {
                $count += $read; $total += $read
                if ($count -gt 268435456 -or $total -gt 1073741824) { throw 'Actual native archive expansion exceeded its bound.' }
                $outputStream.Write($buffer,0,$read)
            }
            if ($count -ne $entry.Length) { throw 'Actual member length mismatch.' }
            $outputStream.Flush($true)
        } finally { $outputStream.Dispose(); $inputStream.Dispose() }
    }
} finally { $zip.Dispose() }
$actualRoot = Join-Path $extracted 'UnityTacticalAiDesigner'
foreach ($entry in $entries) {
    $path = Join-Path $actualRoot $entry.path
    if ((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Extracted native byte mismatch: $($entry.path)" }
}
if (@(Get-ChildItem -LiteralPath $actualRoot -File -Recurse).Count -ne $entries.Count+1) { throw 'Unexpected or missing extracted native payload.' }
if ((Get-FileHash -LiteralPath (Join-Path $actualRoot 'RELEASE.json')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $payload 'RELEASE.json')).Hash) { throw 'Embedded source metadata mismatch.' }
$acceptance = Join-Path $output 'acceptance'
New-Item -ItemType Directory -Path $acceptance | Out-Null
[IO.File]::WriteAllText((Join-Path $acceptance 'acceptance.txt'), "JasonStys/unity-native-acceptance-v1`n", $utf8)
$executable = Join-Path $actualRoot 'TacticalAiDesigner.exe'
foreach ($phase in @(1,2)) {
    $options = [Diagnostics.ProcessStartInfo]::new($executable)
    $options.UseShellExecute=$false; $options.WorkingDirectory=$output; $options.RedirectStandardError=$true; $options.CreateNoWindow=$true
    foreach ($argument in @('--acceptance-root',$acceptance,'--phase',"$phase")) { $options.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::Start($options)
    try {
        $errors = $child.StandardError.ReadToEndAsync()
        if (-not $child.WaitForExit(90000)) { throw "Actual native acceptance is still active; preserve owned PID $($child.Id). No forced cleanup." }
        [IO.File]::WriteAllText((Join-Path $output "native-phase-$phase-stderr.log"),$errors.GetAwaiter().GetResult(),$utf8)
        if ($child.ExitCode -ne 0) { throw "Actual native phase $phase failed with $($child.ExitCode); preserve receipts/logs." }
        $result = Get-Content -LiteralPath (Join-Path $acceptance "phase-$phase.json") -Raw | ConvertFrom-Json
        if ($result.passed -ne $true -or $result.version -ne $version -or $result.windowVisible -ne $true -or $result.acceptedCommands -ne 2) { throw 'Actual native receipt contract failed.' }
    } finally { $child.Dispose() }
}
[IO.File]::WriteAllText((Join-Path $output 'package-acceptance.json'), (@{schema=1;passed=$true;sourceCommit=$source;sourceDirty=[bool]$changed;version=$version;archive=[IO.Path]::GetFileName($archive);archiveSha256=$archiveHash;payloadFiles=$entries.Count;actualRuntimeDllVersion=$runtimeVersion;actualNativePhases=2;scope='Actual Windows x64 self-contained archive, safe fresh extraction and visible owned WinForms journeys only; unsigned, no installer or clean-machine claim.'} | ConvertTo-Json -Depth 6)+"`n",$utf8)
Write-Host "Actual Windows designer package/extraction/two-native-process acceptance passed: $archive"
