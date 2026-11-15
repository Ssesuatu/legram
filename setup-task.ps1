# Registers the silent elevated Scheduled Task 'Legram_PurgeHelper' (one-time UAC approval).
# Once registered, Legram can execute Kernel Page Combine + Low-Priority Standby Purge
# (NtSetSystemInformation 130, 80 & 155) in <150ms with ZERO future UAC prompts and ZERO disk stutter.
param(
    [string]$ExePath = '',
    [switch]$Unregister
)
$ErrorActionPreference = 'Stop'
$taskName = 'Legram_PurgeHelper'

if ($Unregister) {
    Start-Process -FilePath 'schtasks.exe' -ArgumentList "/Delete /TN `"$taskName`" /F" -Verb RunAs -Wait
    Write-Output "Removed scheduled task: $taskName"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $adjacent = Join-Path $PSScriptRoot 'Legram.exe'
    $binSub = Join-Path $PSScriptRoot 'bin\Legram.exe'
    if (Test-Path $adjacent) {
        $ExePath = $adjacent
    } else {
        $ExePath = $binSub
    }
}

$fullExe = [IO.Path]::GetFullPath($ExePath)
if (-not (Test-Path $fullExe)) {
    throw "Legram.exe not found at $fullExe. Run .\build\build.ps1 first."
}

$argList = "/Create /TN `"$taskName`" /TR `"\`"$fullExe\`" --purge-helper`" /SC ONCE /ST 00:00 /RL HIGHEST /F"
Start-Process -FilePath 'schtasks.exe' -ArgumentList $argList -Verb RunAs -Wait

& schtasks.exe /Query /TN $taskName | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Output "Registered '$taskName' -> $fullExe --purge-helper (0-UAC Kernel Standby Purge ready!)"
} else {
    Write-Warning "Scheduled task registration was cancelled or did not complete."
}
