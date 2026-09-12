# purge-build-waste.ps1 -- reclaim disk from build by-products and scratch trees.
#
# WHY THIS EXISTS
#   The E: volume hosting this repo is small (200 GB). A single self-contained
#   WinUI publish plus probe builds can consume >3 GB of obj/bin in minutes, and
#   publish scratch trees (checkouts, staged publish outputs) add several more GB.
#   Run this after every build/publish round, or before starting a new one.
#
# INSTRUMENT NOTES (learned the hard way)
#   1) Do NOT name helper functions Rm/Del/Cp/Mv/Ls/Cat: PowerShell resolves
#      Alias BEFORE Function, and those names are aliases for Remove-Item etc.
#      The call silently goes to Remove-Item and, in a non-interactive host,
#      dies with "Read and Prompt functionality is not available".
#   2) Remove-Item -Recurse also prompts for read-only/hidden attributes even
#      with -Force in some hosts. `cmd /c attrib` + `cmd /c rd /s /q` does not.
#   3) Build outputs must never be deleted while a build is in flight: check the
#      dotnet process count first (a killed mid-build leaves MSB3073 transients).
#
# WHAT IS NEVER DELETED
#   kernel/build/**  -> holds the Release kernel artifact (AIPlayer.MpvHost.dll)
#                       that the publish face copies into player\.
#   dist\AIPlayer    -> the live published app.
#   dist\AIPlayer.bak-* -> only the newest one is kept as a rollback point.
#
# USAGE
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell\tools\purge-build-waste.ps1 -DryRun
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell\tools\purge-build-waste.ps1

[CmdletBinding()]
param(
    [string]   $RepoRoot = 'E:\AI Player',
    [string[]] $ScratchRoots = @(
        'E:\publish-head',
        'E:\publish-out-v5',
        'E:\publish-out-v6',
        'E:\publish-out-v7',
        'E:\t136-fixed',
        'E:\t187-face',
        'E:\fork-player-stage-t130'
    ),
    [switch]   $KeepNewestDistBackup = $true,
    [switch]   $DryRun
)

$ErrorActionPreference = 'Continue'

function Get-TreeMB {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { return -1 }
    $sum = (Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
    return [math]::Round($sum / 1MB, 1)
}

function Remove-Tree {
    param([string] $Path, [switch] $DryRun)
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    $mb = Get-TreeMB -Path $Path
    if ($DryRun) {
        Write-Host ("  [dry-run] {0} = {1} MB" -f $Path, $mb)
        return $mb
    }
    cmd /c "attrib -r -h -s /s /d `"$Path\*`" >nul 2>&1"
    cmd /c "rd /s /q `"$Path`" >nul 2>&1"
    if (Test-Path -LiteralPath $Path) {
        Write-Host ("  [FAILED ] {0} = {1} MB (in use?)" -f $Path, $mb)
        return 0
    }
    Write-Host ("  [purged ] {0} = {1} MB" -f $Path, $mb)
    return $mb
}

$drive = (Get-Item -LiteralPath $RepoRoot).PSDrive.Name
$freeBefore = (Get-PSDrive $drive).Free
$freedMB = 0.0

Write-Host ("volume {0}: free before = {1:N2} GB" -f $drive, ($freeBefore / 1GB))

$dotnet = @(Get-Process dotnet -ErrorAction SilentlyContinue).Count
Write-Host ("dotnet processes = {0}" -f $dotnet)
# SAFETY (2026-09-12, reported by ui3): the guard used to wrap ONLY section 1, so
# sections 2/3 (publish scratch trees, old dist backups) ran unconditionally and
# could delete another member's in-flight publish trees. The guard now covers the
# WHOLE script: with any dotnet process alive we refuse everything and return.
# Rationale: `dotnet` count is noisy (idle nodes / in-flight builds are not
# distinguishable), so "maybe someone is publishing" must fail closed.
if ($dotnet -gt 0) {
    Write-Host 'REFUSING to purge ANYTHING while a build may be in flight.'
    Write-Host 'The guard covers ALL sections (1 repo obj/bin, 2 scratch trees, 3 old dist backups).'
    Write-Host 'Re-run when the dotnet process count is 0.'
    return
}
if ($true) {
    Write-Host '=== 1) repo obj/bin (except kernel\build) ==='
    $targets = Get-ChildItem -LiteralPath $RepoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -in @('bin', 'obj') -and
            $_.FullName -notmatch '\\\.git\\' -and
            $_.FullName -notmatch '\\kernel\\build\\'
        }
    Write-Host ("  candidates = {0}" -f @($targets).Count)
    foreach ($t in $targets) { $freedMB += (Remove-Tree -Path $t.FullName -DryRun:$DryRun) }
}

Write-Host '=== 2) publish/scratch trees ==='
foreach ($p in $ScratchRoots) { $freedMB += (Remove-Tree -Path $p -DryRun:$DryRun) }

Write-Host '=== 3) old dist backups (keep newest as rollback) ==='
$distDir = Join-Path $RepoRoot 'dist'
if (Test-Path -LiteralPath $distDir) {
    $baks = @(Get-ChildItem -LiteralPath $distDir -Directory -Force -ErrorAction SilentlyContinue |
              Where-Object { $_.Name -like 'AIPlayer.bak-*' } |
              Sort-Object LastWriteTime -Descending)
    if ($KeepNewestDistBackup -and $baks.Count -gt 0) {
        Write-Host ("  keeping rollback point: {0}" -f $baks[0].Name)
        $baks = @($baks | Select-Object -Skip 1)
    }
    foreach ($b in $baks) { $freedMB += (Remove-Tree -Path $b.FullName -DryRun:$DryRun) }
}

$freeAfter = (Get-PSDrive $drive).Free
Write-Host ('=== RESULT ===')
Write-Host ("purged {0:N2} GB ; free {1:N2} GB -> {2:N2} GB" -f ($freedMB / 1024), ($freeBefore / 1GB), ($freeAfter / 1GB))
