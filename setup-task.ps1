# Registers the silent elevated Scheduled Task 'Legram_PurgeHelper' (one-time UAC approval).
# Once registered, Legram can execute Kernel Page Combine + Low-Priority Standby Purge
# (NtSetSystemInformation 130, 80 & 155) in <150ms on both AC and laptop battery power
# with ZERO future UAC prompts and ZERO disk stutter.
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

$escapedExe = [Security.SecurityElement]::Escape($fullExe)
$tempXml = Join-Path ([IO.Path]::GetTempPath()) ("legram-task-" + [Guid]::NewGuid().ToString('N') + '.xml')

$taskXml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>Legram 0-UAC Kernel Page Combine and Low-Priority Standby Purge Helper</Description>
  </RegistrationInfo>
  <Triggers />
  <Principals>
    <Principal id="Author">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT1M</ExecutionTimeLimit>
    <Priority>5</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$escapedExe</Command>
      <Arguments>--purge-helper</Arguments>
    </Exec>
  </Actions>
</Task>
"@

try {
    [IO.File]::WriteAllText($tempXml, $taskXml, [Text.Encoding]::Unicode)
    $argList = "/Create /TN `"$taskName`" /XML `"$tempXml`" /F"
    Start-Process -FilePath 'schtasks.exe' -ArgumentList $argList -Verb RunAs -Wait
}
finally {
    if (Test-Path $tempXml) { Remove-Item $tempXml -Force -ErrorAction SilentlyContinue }
}

& schtasks.exe /Query /TN $taskName | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Output "Registered '$taskName' -> $fullExe --purge-helper (0-UAC Kernel Standby Purge ready on AC & Battery!)"
} else {
    Write-Warning "Scheduled task registration was cancelled or did not complete."
}
