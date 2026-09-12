# t26 evidence loop: retry the evidence run until the full evidence set is present.
# Why a loop: during this attempt two other agents were launching/killing the shell exe on the
# same machine, and one of them clicked my window (log: Nav -> servers / Nav -> settings while
# my window was up) and one killed my process 2 s after startup (EXITED code=-1). The run is
# therefore made repeatable and self-checking instead of hand-observed.
# ASCII-only script text on purpose.
param(
  [string]$Repo = 'E:\AI Player',
  [int]$MaxAttempts = 6,
  [int]$QuietSeconds = 20,
  [int]$WaitSec = 18,
  [string]$PrefixBase = 'ui-t26-attempt3-try'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $Repo

$log = 'shell/Tests/evidence/ui-t26-build-attempt3.log'
$exe = (Resolve-Path 'shell/App/bin/Debug/net9.0-windows10.0.22621.0/win-x64/AIPlayer.Shell.exe').Path
$need = @('RailBg#202020', 'PageBg#222222', 'ControlBg#2F2F2F', 'RowSelectedBg#2D2D2D', 'Accent#9D82C2')

for ($i = 1; $i -le $MaxAttempts; $i++) {
  "LOOP attempt=$i/$MaxAttempts start=$((Get-Date).ToString('HH:mm:ss')) HEAD=$(git rev-parse --short HEAD)" |
    Tee-Object -FilePath $log -Append

  # quiet window
  $deadline = (Get-Date).AddSeconds(300)
  $quietStart = $null
  while ((Get-Date) -lt $deadline) {
    $busy = @(Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue)
    if ($busy.Count -gt 0) { $quietStart = $null }
    else {
      if ($null -eq $quietStart) { $quietStart = Get-Date }
      elseif (((Get-Date) - $quietStart).TotalSeconds -ge $QuietSeconds) { break }
    }
    Start-Sleep -Seconds 3
  }

  $prefix = "$PrefixBase$i"
  & powershell -NoProfile -ExecutionPolicy Bypass -File 'shell/App/tools/t26-evidence.ps1' `
    -Exe $exe -OutDir 'shell/Tests/evidence' -WaitSec $WaitSec -NamePrefix $prefix 2>&1 |
    Tee-Object -FilePath $log -Append | Out-Null

  $ef = "shell/Tests/evidence/$prefix-evidence.txt"
  if (-not (Test-Path -LiteralPath $ef)) {
    "LOOP attempt=$i RESULT=NO-EVIDENCE-FILE" | Tee-Object -FilePath $log -Append
    Start-Sleep -Seconds 5
    continue
  }
  # 必须按 UTF-8 读：本文件含中文（服务器名、tooltip、UIA-PIXEL text='…'）。PS 5.1 的 Get-Content 会走
  # ANSI/GBK，遇到多字节字符会把紧随的 0x0A 当 trail byte 吞掉 ⇒ **静默少行** ⇒ 下面的完整性断言可能在
  # 证据其实不完整时通过（假 PASS），比乱码危险得多。实测同族案例：shell/docs/VERIFY_S1.md 真值 1981 行、
  # Get-Content 只读到 1348 行。
  $txt = [System.IO.File]::ReadAllText($ef, [System.Text.Encoding]::UTF8)
  $missing = @()
  foreach ($n in $need) {
    if ($txt -notmatch ("POINT(-AFTER)? " + [regex]::Escape($n) + " [^\r\n]*HIT=True")) { $missing += $n }
  }
  if ($txt -notmatch 'RAILSCAN [^\r\n]*railWidthPx=256') { $missing += 'railWidth256' }
  if ($txt -notmatch 'STATUS-SQUARES count=[1-9]') { $missing += 'statusSquares' }
  if ($txt -notmatch 'UIA-RAIL-TEXTS count=[1-9]') { $missing += 'uiaRailTexts' }
  if ($txt -notmatch "UIA-PIXEL text='[^']+'") { $missing += 'uiaPixelHistogram' }

  $after = @($txt | Select-String -Pattern 'POINT-AFTER' -AllMatches).Count
  if ($missing.Count -eq 0) {
    "LOOP attempt=$i RESULT=COMPLETE file=$ef afterReselectProbes=$after" | Tee-Object -FilePath $log -Append
    Write-Host "LOOP RESULT=COMPLETE file=$ef"
    exit 0
  }
  "LOOP attempt=$i RESULT=INCOMPLETE missing=$($missing -join ',') afterReselectProbes=$after" | Tee-Object -FilePath $log -Append
  Start-Sleep -Seconds 5
}

"LOOP RESULT=EXHAUSTED attempts=$MaxAttempts" | Tee-Object -FilePath $log -Append
Write-Host "LOOP RESULT=EXHAUSTED"
exit 2
