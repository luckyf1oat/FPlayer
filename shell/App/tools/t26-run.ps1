# t26 evidence driver: wait for a quiet window (nobody else is running the shell exe, which
# locks bin\...\win-x64\AIPlayer.Shell.Services.dll and makes the build fail with MSB3027),
# then run the card's verify command, then do the pixel evidence run.
# ASCII-only script text on purpose.
param(
  [string]$Repo = 'E:\AI Player',
  [int]$QuietSeconds = 15,
  [int]$MaxWaitSeconds = 900,
  [string]$NamePrefix = 'ui-t26-attempt3'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $Repo

$log = 'shell/Tests/evidence/ui-t26-build-attempt3.log'
$startStamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
$head = (git rev-parse --short HEAD)
"=== DRIVER START $startStamp HEAD=$head ===" | Out-File -Encoding utf8 $log

# --- 1. quiet window ---
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

# --- 2. the card's verify command ---
$b0 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
"=== BUILD START $b0 HEAD=$(git rev-parse --short HEAD) ===" | Tee-Object -FilePath $log -Append
$out = & dotnet build shell/App/AIPlayer.Shell.csproj -c Debug -v m 2>&1
$code = $LASTEXITCODE
$out | Out-File -Encoding utf8 -Append $log
$b1 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
"=== BUILD END $b1 EXIT=$code ===" | Tee-Object -FilePath $log -Append
$errs = @($out | Select-String -Pattern ': error ' -SimpleMatch)
$warns = @($out | Select-String -Pattern ' warning ' -SimpleMatch)
"BUILD-READING exit=$code errors=$($errs.Count) warning_lines=$($warns.Count) head=$(git rev-parse --short HEAD) at=$b1" | Tee-Object -FilePath $log -Append
if ($code -ne 0) {
  Write-Host "BUILD FAILED - evidence run skipped"
  $errs | Select-Object -First 20 | ForEach-Object { Write-Host ("  " + $_.Line) }
  exit 1
}

# --- 3. pixel evidence run ---
$exe = (Resolve-Path 'shell/App/bin/Debug/net9.0-windows10.0.22621.0/win-x64/AIPlayer.Shell.exe').Path
"EVIDENCE-INVOKE exe=$exe at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append
& powershell -NoProfile -ExecutionPolicy Bypass -File 'shell/App/tools/t26-evidence.ps1' -Exe $exe -OutDir 'shell/Tests/evidence' -WaitSec 22 -NamePrefix $NamePrefix 2>&1 |
  Tee-Object -FilePath $log -Append
"EVIDENCE-EXIT=$LASTEXITCODE at=$((Get-Date).ToString('HH:mm:ss'))" | Tee-Object -FilePath $log -Append
exit 0
