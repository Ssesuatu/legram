# Builds bin\Legram.exe with the native .NET Framework 4.x C# compiler (csc.exe).
# Zero NuGet, zero Electron, zero external SDKs — ~208 KB native x64 binary.
param([string]$Out = (Join-Path $PSScriptRoot '..\bin'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Out = [IO.Path]::GetFullPath($Out)
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be MAJOR.MINOR.PATCH, got '$version'" }

$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fx 'csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc (.NET Framework 4.x is required)" }
$wpf = Join-Path $fx 'WPF'

$icoPath = Join-Path $root 'assets\legram.ico'
$pngPath = Join-Path $root 'assets\legram-256.png'
$icoScript = Join-Path $PSScriptRoot 'generate-icon.ps1'
if ((-not (Test-Path $icoPath)) -or ((Get-Item $icoScript).LastWriteTimeUtc -gt (Get-Item $icoPath).LastWriteTimeUtc)) {
    & $icoScript -IcoPath $icoPath -PngPath $pngPath
}

$gen = Join-Path $Out 'obj'
New-Item -ItemType Directory -Force $Out, $gen | Out-Null

$info = Get-Content (Join-Path $root 'src\AssemblyInfo.cs') -Raw
$info = $info -replace 'Version\("[0-9.]+"\)', ('Version("' + $version + '.0")') -replace 'InformationalVersion\("[0-9.]+"\)', ('InformationalVersion("' + $version + '")')
$infoGen = Join-Path $gen 'AssemblyInfo.g.cs'
[IO.File]::WriteAllText($infoGen, $info, (New-Object Text.UTF8Encoding($true)))

$manifest = Get-Content (Join-Path $root 'assets\app.manifest') -Raw
$manifest = $manifest -replace '(<assemblyIdentity\b[^>]*\bversion=")[0-9.]+(")', ('${1}' + $version + '.0${2}')
$manifestGen = Join-Path $gen 'app.manifest'
[IO.File]::WriteAllText($manifestGen, $manifest, (New-Object Text.UTF8Encoding($false)))

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xaml.dll',
    'System.Management.dll'
) | ForEach-Object { "/reference:$fx\$_" }
$refs += @(
    'WindowsBase.dll',
    'PresentationCore.dll',
    'PresentationFramework.dll'
) | ForEach-Object { "/reference:$wpf\$_" }

$src = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs -Recurse | Where-Object Name -ne 'AssemblyInfo.cs' | ForEach-Object FullName) + $infoGen

& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 /utf8output `
    "/win32manifest:$manifestGen" "/win32icon:$icoPath" "/out:$Out\Legram.exe" $refs $src
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$fontsSrc = Join-Path $root 'assets\fonts'
if (Test-Path $fontsSrc) {
    New-Item -ItemType Directory -Force (Join-Path $Out 'fonts') | Out-Null
    Copy-Item (Join-Path $fontsSrc '*') (Join-Path $Out 'fonts') -Force
}
Copy-Item $icoPath $Out -Force
Copy-Item (Join-Path $root 'LICENSE') $Out -Force
$tpn = Join-Path $root 'THIRD_PARTY_NOTICES.md'
if (Test-Path $tpn) { Copy-Item $tpn $Out -Force }
$setupTask = Join-Path $root 'setup-task.ps1'
if (Test-Path $setupTask) { Copy-Item $setupTask $Out -Force }
Remove-Item $gen -Recurse -Force

$exeItem = Get-Item "$Out\Legram.exe"
Write-Output ("Built {0} ({1}, {2:F1} KB)" -f $exeItem.FullName, $version, ($exeItem.Length / 1024.0))
