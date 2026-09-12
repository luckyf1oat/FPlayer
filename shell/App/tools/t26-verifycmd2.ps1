# t26 verify command (exact card wording) with concurrency-aware retry.
# Kept separate from t26-verifycmd.ps1 because that one mixed Tee-Object (PowerShell default
# UTF-16) with Out-File -Encoding utf8 into the same file and produced an unreadable log.
# ASCII-only script text.
param(
  [string]$Repo = 'E:\AI Player',
  [int]$MaxAttempts = 6,
  [int]$QuietSeconds = 20,
  [string]$Log = 'shell/Tests/evidence/ui-t26-verifycmd.log'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $Repo

function Log([string]$s) {
  $s | Out-File -LiteralPath $Log -Encoding utf8 -Append
  Write-Host $s
}

Log ("=== VERIFY-CMD LOG START " + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + " HEAD=" + (git rev-parse --short HEAD) + " ===")

for ($i = 1; $i -le $MaxAttempts; $i++) {
  $deadline = (Get-Date).AddSeconds(240)
  $quietStart = $null
  while ((Get-Date) -lt $deadline) {
    $busy = @(Get-Process -Name 'dotnet', 'MSBuild', 'XamlCompiler', 'VBCSCompiler' -ErrorAction SilentlyContinue)
    if ($busy.Count -gt 0) { $quietStart = $null }
    else {
      if ($null -eq $quietStart) { $quietStart = Get-Date }
      elseif (((Get-Date) - $quietStart).TotalSeconds -ge $QuietSeconds) { break }
    }
    Start-Sleep -Seconds 3
  }

  $t0 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
  $head = (git rev-parse --short HEAD)
  Log ("=== VERIFY-CMD ATTEMPT $i START $t0 HEAD=$head CMD=dotnet build shell/App/AIPlayer.Shell.csproj -c Debug ===")
  $out = & dotnet build shell/App/AIPlayer.Shell.csproj -c Debug 2>&1
  $code = $LASTEXITCODE
  $t1 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
  $text = ($out | ForEach-Object { "$_" }) -join "`r`n"
  Log $text
  $errs = @($out | Select-String -Pattern ': error ' -SimpleMatch)
  $sum = ($out | Select-String -Pattern '个错误|个警告' | Select-Object -Last 2 | ForEach-Object { $_.Line.Trim() }) -join ' / '
  Log ("=== VERIFY-CMD ATTEMPT $i END $t1 EXIT=$code ERRORS=$($errs.Count) SUMMARY=$sum ===")

  if ($code -eq 0) { Write-Host "VERIFY-CMD OK attempt=$i exit=0"; exit 0 }
  $locked = @($out | Select-String -Pattern 'CS2012|MSB3027|MSB3021|being used by another process' -SimpleMatch)
  if ($locked.Count -eq 0) { Write-Host "VERIFY-CMD FAILED-NOT-LOCK attempt=$i exit=$code"; exit 1 }
  Write-Host "VERIFY-CMD RETRY attempt=$i (build output locked by a concurrent build)"
  Start-Sleep -Seconds 20
}
Write-Host 'VERIFY-CMD EXHAUSTED'
exit 2
