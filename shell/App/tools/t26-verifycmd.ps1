# Run the t26 verify command EXACTLY as the card writes it, once no other build is running.
# The first attempt failed with CS2012 (intermediatexaml\AIPlayer.Shell.dll locked by
# Microsoft.UI.Xaml.Markup.Compiler pid 19864) -- a concurrent build, not a code error.
# ASCII-only script text.
param(
  [string]$Repo = 'E:\AI Player',
  [int]$MaxAttempts = 8,
  [int]$QuietSeconds = 15
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $Repo
$log = 'shell/Tests/evidence/ui-t26-verifycmd.log'

for ($i = 1; $i -le $MaxAttempts; $i++) {
  # wait until no compiler/build process is running
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
  "=== VERIFY-CMD ATTEMPT $i START $t0 HEAD=$head ===" | Tee-Object -FilePath $log -Append
  $out = & dotnet build shell/App/AIPlayer.Shell.csproj -c Debug 2>&1
  $code = $LASTEXITCODE
  $out | Out-File -Encoding utf8 -Append $log
  $t1 = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
  $errs = @($out | Select-String -Pattern ': error ' -SimpleMatch)
  "=== VERIFY-CMD ATTEMPT $i END $t1 EXIT=$code ERRORS=$($errs.Count) ===" | Tee-Object -FilePath $log -Append
  ($out | Select-String -Pattern '\d+ 个警告|\d+ 个错误|已成功生成' | Select-Object -Last 3) | ForEach-Object { Tee-Object -InputObject $_.Line -FilePath $log -Append }

  if ($code -eq 0) { Write-Host "VERIFY-CMD OK attempt=$i exit=0"; exit 0 }
  $locked = @($out | Select-String -Pattern 'CS2012|MSB3027|MSB3021|being used by another process|locked' -SimpleMatch)
  if ($locked.Count -eq 0) { Write-Host "VERIFY-CMD FAILED (not a lock) attempt=$i exit=$code"; exit 1 }
  Write-Host "VERIFY-CMD retry (locked by concurrent build) attempt=$i"
  Start-Sleep -Seconds 20
}
Write-Host 'VERIFY-CMD EXHAUSTED'
exit 2
