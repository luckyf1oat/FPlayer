# t26 evidence re-run driver: no build (the build reading for this attempt already exists),
# only the pixel/UI-Automation run, and only once nobody else is driving the shell exe.
# Reason: the 23:00:40 run was clicked by a second agent (its log shows Nav -> servers /
# Nav -> settings at 23:00:54-55 while my window was up), which cleared the ListView row
# selection and made the selected-row / accent-bar samples miss. ASCII-only script text.
param(
  [string]$Repo = 'E:\AI Player',
  [int]$QuietSeconds = 30,
  [int]$MaxWaitSeconds = 900,
  [string]$NamePrefix = 'ui-t26-attempt3-clean'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $Repo

$log = 'shell/Tests/evidence/ui-t26-build-attempt3.log'
"CLEAN-RERUN-DRIVER start=$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')) HEAD=$(git rev-parse --short HEAD)" |
  Tee-Object -FilePath $log -Append

$deadline = (Get-Date).AddSeconds($MaxWaitSeconds)
$quietStart = $null
while ((Get-Date) -lt $deadline) {
  $busy = @(Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue)
  if ($busy.Count -gt 0) {
    $ids = ($busy | ForEach-Object { $_.Id }) -join ','
    "WAIT-BUSY pid=$ids at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append
    $quietStart = $null
  } else {
    if ($null -eq $quietStart) { $quietStart = Get-Date; "QUIET-BEGIN at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append }
    elseif (((Get-Date) - $quietStart).TotalSeconds -ge $QuietSeconds) { break }
  }
  Start-Sleep -Seconds 3
}
"QUIET-CONFIRMED at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append

$exe = (Resolve-Path 'shell/App/bin/Debug/net9.0-windows10.0.22621.0/win-x64/AIPlayer.Shell.exe').Path
"EVIDENCE-INVOKE exe=$exe at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append
& powershell -NoProfile -ExecutionPolicy Bypass -File 'shell/App/tools/t26-evidence.ps1' `
  -Exe $exe -OutDir 'shell/Tests/evidence' -WaitSec 18 -NamePrefix $NamePrefix 2>&1 |
  Tee-Object -FilePath $log -Append
"EVIDENCE-EXIT=$LASTEXITCODE at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append
exit 0
