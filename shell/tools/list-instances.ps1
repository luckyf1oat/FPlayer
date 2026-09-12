# list-instances.ps1 -- the ONE canonical way to answer "who is running a shell right now?".
#
# WHY (captain 2026-09-12, after ~40 minutes of capture windows were burned): a capture needs a moment with
# instances=0, and three separate times an instance was mis-attributed ("is that the user's window? is it a
# teammate's?") because every report used a different ad-hoc command. The captain's standing rule is now:
#   whoever starts a shell window must use an ISOLATED output dir (E:\<name>-bin\) and must be able to report
#   pid + path + start time on request.
# This script prints exactly those three fields per instance, plus the parent directory that hints at the
# owner -- and it NEVER guesses ownership (that stays a human confirmation, per hygiene-owner-map discipline).
#
# WHY -Samples EXISTS (measured, same night): the first run of this tool saw shells=0 at 04:02:33; a bare
# re-run at 04:03:01 saw ONE live instance (E:\ui3-search-bin\AIPlayer.Shell.exe, pid 12808, started
# 04:02:36.470) and the very next run at 04:03:04 saw shells=0 again -- i.e. that instance lived ~25-28 s.
# A single reading is therefore NOT evidence of a free field; it is a snapshot of a field that churns on the
# order of tens of seconds. Use -Samples N to make a defensible reading.
#
# Invoke (the host blocks .ps1 by policy; there is no pwsh here):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1 -Samples 10 -IntervalMs 3000
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1 -Json $env:TEMP\li.json
#
# EXIT: 0 always (this is a reading, not a gate). Rule learned tonight: the EXIT code is not a verdict --
# read the TOOL-START / INSTANCE / FREE-FIELD anchors instead. A rule-level gate decision must never be
# derived from the process exit code alone.

[CmdletBinding()]
param(
    # Number of readings. 1 = snapshot (NOT a free-field proof); N>1 = a real observation window.
    [int]$Samples = 1,
    # Gap between readings, in milliseconds. Only used when -Samples is greater than 1.
    [int]$IntervalMs = 1000,
    # Optional path for a JSON copy of the SAME reading (the summary, not every sample).
    # NOTE: this MUST stay a [string], not a [switch] -- the first draft declared it a switch and
    # `-Json <path>` died with "A positional parameter cannot be found that accepts argument '<path>'",
    # EXIT=1, no file written. Caught only by running it.
    [string]$Json = ''
)

$ErrorActionPreference = 'Continue'

function Say([string]$s) { Write-Output $s }
function Now() { return (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') }

if ($Samples -lt 1) { $Samples = 1 }
if ($IntervalMs -lt 0) { $IntervalMs = 0 }

$head = ''
try { $head = ((& git -C (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path rev-parse --short HEAD 2>$null) | Select-Object -First 1) } catch { $head = '' }
if ([string]::IsNullOrWhiteSpace($head)) { $head = '<no-git-head>' }
Say ("TOOL-START|script=list-instances.ps1|ps=" + $PSVersionTable.PSVersion.ToString() + "|time=" + (Now) + "|head=" + $head + "|cwd=" + (Get-Location).Path + "|samples=" + $Samples + "|intervalMs=" + $IntervalMs)

function Get-Instances([string]$name) {
    $rows = @()
    foreach ($p in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
        $path = ''; $start = ''; $title = ''
        try { $path = $p.Path } catch { $path = '<access denied>' }
        try { $start = $p.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fff') } catch { $start = '<unknown>' }
        try { $title = $p.MainWindowTitle } catch { $title = '' }
        $ownerDir = ''
        if ($path -ne '' -and $path -ne '<access denied>') { $ownerDir = Split-Path -Parent $path }
        $rows += , @{ Id = $p.Id; Path = $path; Start = $start; Title = $title; OwnerDir = $ownerDir }
    }
    return $rows
}

# Per-pid ledger so an instance that appears and dies inside the window is still reported at the end.
$ledger = @{}
function Note-Seen($rows) {
    foreach ($r in $rows) {
        $k = [string]$r.Id
        if (-not $ledger.ContainsKey($k)) {
            $ledger[$k] = @{ Id = $r.Id; Path = $r.Path; OwnerDir = $r.OwnerDir; Start = $r.Start; First = (Now); Last = (Now); Hits = 0 }
        }
        $ledger[$k].Last = (Now)
        $ledger[$k].Hits = $ledger[$k].Hits + 1
    }
}

$windowStart = Now
$maxShells = 0; $maxKernels = 0; $allFree = $true; $freeSamples = 0
$lastShells = @(); $lastKernels = @(); $lastBuilders = @(0, 0, 0)

for ($i = 1; $i -le $Samples; $i++) {
    $shells = @(Get-Instances 'AIPlayer.Shell')
    $kernels = @(Get-Instances 'AIPlayer.MpvHost')
    $builders = @()
    foreach ($n in @('dotnet', 'MSBuild', 'XamlCompiler')) { $builders += @(Get-Process -Name $n -ErrorAction SilentlyContinue).Count }

    $thisFree = (($shells.Count -eq 0) -and ($kernels.Count -eq 0))
    if ($thisFree) { $freeSamples = $freeSamples + 1 } else { $allFree = $false }
    if ($shells.Count -gt $maxShells) { $maxShells = $shells.Count }
    if ($kernels.Count -gt $maxKernels) { $maxKernels = $kernels.Count }

    $pids = @($shells | ForEach-Object { $_.Id }) -join ','
    Say ("SAMPLE|" + $i + "/" + $Samples + "|time=" + (Now) + "|shells=" + $shells.Count + "|kernels=" + $kernels.Count + "|free=" + $thisFree + "|shellPids=" + $pids)

    foreach ($r in ($shells | Sort-Object { $_.Start })) {
        Say ("INSTANCE|sample=" + $i + "/" + $Samples + "|pid=" + $r.Id + "|path=" + $r.Path + "|start=" + $r.Start + "|ownerDir=" + $r.OwnerDir + "|title=" + $r.Title + "|note=ownerDir is a HINT, never a verdict; ownership is confirmed by the owner (pid+path+start)")
    }
    foreach ($r in ($kernels | Sort-Object { $_.Start })) {
        Say ("KERNEL|sample=" + $i + "/" + $Samples + "|pid=" + $r.Id + "|path=" + $r.Path + "|start=" + $r.Start + "|ownerDir=" + $r.OwnerDir + "|title=" + $r.Title)
    }
    Note-Seen $shells
    Note-Seen $kernels

    $lastShells = $shells; $lastKernels = $kernels; $lastBuilders = $builders
    if ($i -lt $Samples -and $IntervalMs -gt 0) { Start-Sleep -Milliseconds $IntervalMs }
}
$windowEnd = Now

foreach ($k in ($ledger.Keys | Sort-Object { [int]$_ })) {
    $e = $ledger[$k]
    Say ("SEEN|pid=" + $e.Id + "|path=" + $e.Path + "|exeStart=" + $e.Start + "|firstSeen=" + $e.First + "|lastSeen=" + $e.Last + "|samplesSeen=" + $e.Hits + "/" + $Samples)
}

Say ("BUILDERS|dotnet=" + $lastBuilders[0] + "|msbuild=" + $lastBuilders[1] + "|xamlcompiler=" + $lastBuilders[2] + "|note=builds do NOT block a capture (soft warning only); they are reported here so a reader is never surprised")
Say ("INSTANCES|shells=" + $lastShells.Count + "|kernels=" + $lastKernels.Count + "|free=" + (($lastShells.Count -eq 0) -and ($lastKernels.Count -eq 0)) + "|sampled=" + $windowEnd)

$spanSec = [math]::Round((New-TimeSpan -Start ([datetime]::ParseExact($windowStart, 'yyyy-MM-dd HH:mm:ss.fff', $null)) -End ([datetime]::ParseExact($windowEnd, 'yyyy-MM-dd HH:mm:ss.fff', $null))).TotalSeconds, 2)
$verdict = if ($allFree) { 'FREE-OVER-WINDOW' } elseif ($Samples -eq 1) { 'SNAPSHOT-OCCUPIED' } else { 'OCCUPIED-AT-SOME-POINT' }
$note = if ($Samples -eq 1) { 'single reading: NOT a free-field proof (the field churns on the order of tens of seconds)' } else { 'allFree=true over this window is the reading a capture-quality free-field claim needs; it says nothing about any instant outside the window' }
Say ("FREE-FIELD|verdict=" + $verdict + "|samples=" + $Samples + "|freeSamples=" + $freeSamples + "|allFree=" + $allFree + "|maxShells=" + $maxShells + "|maxKernels=" + $maxKernels + "|distinctPids=" + $ledger.Keys.Count + "|windowStart=" + $windowStart + "|windowEnd=" + $windowEnd + "|spanSec=" + $spanSec + "|note=" + $note)

if ($Json -ne '') {
    $seen = @()
    foreach ($k in ($ledger.Keys | Sort-Object { [int]$_ })) { $seen += $ledger[$k] }
    $obj = @{
        head = $head
        samples = $Samples; intervalMs = $IntervalMs
        windowStart = $windowStart; windowEnd = $windowEnd; spanSec = $spanSec
        verdict = $verdict; allFree = $allFree; freeSamples = $freeSamples
        maxShells = $maxShells; maxKernels = $maxKernels
        seen = $seen
        finalShells = $lastShells; finalKernels = $lastKernels
        builders = @{ dotnet = $lastBuilders[0]; msbuild = $lastBuilders[1]; xamlcompiler = $lastBuilders[2] }
    }
    # WriteAllText with a BOM-less UTF8Encoding: Set-Content -Encoding UTF8 emits a BOM on PS 5.1 and the
    # BOM then trips strict JSON readers. Cheap, and it keeps the file readable by any consumer.
    [IO.File]::WriteAllText($Json, ($obj | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
    Say ("JSON|" + $Json)
}
exit 0
