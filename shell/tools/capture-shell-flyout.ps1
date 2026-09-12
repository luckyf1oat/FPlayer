# capture-shell-flyout.ps1 -- orchestrate a flyout capture WITHOUT opening a second capture channel.
#
# WHY: ui3's t30 hook (SHELL_SELFTEST_SERVERMENU=1) pops the first server-row context menu
# programmatically, so the menu is on screen with no mouse and no manual step. This script only
#   1) starts the prebuilt shell exe with that env var,
#   2) polls `popup-capture.ps1 -List` until the flyout HWND appears,
#   3) captures it via `popup-capture.ps1 -Hwnd ...` (PrintWindow only -- the approved channel),
#   4) verifies the PNG is not blank INDEPENDENTLY (pixel sampling, not the tool's self-report),
#   5) stops the shell again.
# It never calls CopyFromScreen / BitBlt itself and never uses -Auto (which is popup-shaped but would
# guess at a candidate; an explicit -Hwnd is what we want for evidence).
#
# NEGATIVE CONTROL: -Mode none starts the SAME exe with the SAME page but WITHOUT the hook env var and
# requires that NO menu popup appears -- that is what proves the menu came from the hook.
#
# Invoke (this host blocks .ps1 by policy => always -ExecutionPolicy Bypass). The output name is a
# PLACEHOLDER on purpose: H2 is a plain-text criterion and cannot tell an EXAMPLE from a CITATION, so a
# real-looking evidence path written here would be read as "cited but missing" (I tripped that 4 times).
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/capture-shell-flyout.ps1 `
#       -Exe E:\ui3-verify-bin\AIPlayer.Shell.exe -Out <path>.png
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/capture-shell-flyout.ps1 -Mode none
#
# EXIT: 0 = captured (or, for -Mode none, correctly absent); 1 = failed; 2 = usage/refused; 3 = busy.
#
# PRE-RUN CRITERIA (captain 2026-09-12; SUPERSEDES the older rule "the dll must equal B156252C3771",
# which self-destructs because SEAM2 legitimately lets the UI owners edit features between builds):
#   1. instances >= 1 => REFUSED + -Confirmed is mandatory (both implemented below, unchanged).
#   2. capture-time identity = dllSha256_12 + dllMtime + head + capturedAt, AND a DUAL-ENCODING scan of
#      THAT dll for "MENU-SELFTEST ARMED" and "ServerIconSource": both must HIT. A negative verdict is
#      only valid with BOTH readings (ascii=-1 AND utf16=-1); the hit encoding is printed.
#   3. source-sync: the two ServersPage files must still carry the pinned mtimes (else the menu UI in the
#      image may not be the source on disk) => REFUSED, report the difference, do not capture.
#   4. unchanged: FG-SETTLED, independent pixel verification, stop the instance, POST-STOP|instances=0.
# Together (2)+(3) prove "the image belongs to this dll" AND "this dll's server-menu UI is the same source
# as the working tree" -- strictly stronger than waiting for one fixed hash.

[CmdletBinding()]
param(
    # t151: NO machine-private default. The old default ('E:\ui3-verify-bin\AIPlayer.Shell.exe') exists only on
    # the machine that wrote this tool, so a caller who forgot -Exe silently captured someone else's build. The
    # guard below (placed AFTER the -SelfTestOutcome early exit) refuses the run when -Exe is empty.
    [string]$Exe = '',
    # t151 SANDBOX: the shell must be started with its OWN data root. The guard refuses to run when this is empty
    # or when it points inside the real %LOCALAPPDATA%\AIPlayer -- a capture tool must never write the user root.
    [string]$SandboxRoot = '',
    [string]$Out = '',
    [string]$Sidecar = '',
    [string]$StartPage = 'servers',
    [ValidateSet('hook', 'none')][string]$Mode = 'hook',
    [int]$TimeoutSec = 30,
    # SOURCE GENERATION PIN (criterion v3, captain 2026-09-12). Re-pinning is a CAPTAIN decision, so it is a
    # PARAMETER and not a source edit: pass -SyncCsSha / -SyncXamlSha to move the generation without touching
    # this file, and the run prints which generation it used (SOURCE-GEN-PIN) so no reading is ambiguous.
    [string]$SyncCsSha = '8AA466F0F6B8',
    [string]$SyncXamlSha = 'C85AC26D4DF8',
    # CODE-SCAN needles (criterion 2), semicolon-separated.
    # captain 2026-09-12 ruling: the pair is MENU-SELFTEST ARMED + ServerRowContextMenu -- a marker must be
    # stable across EVERY generation you intend to capture (ServerIconSource died with the crash fix).
    [string]$ScanNeedles = 'MENU-SELFTEST ARMED;ServerRowContextMenu',
    # RETIRED NEEDLES (captain 2026-09-12): a MISS must say WHICH KIND of MISS it is -- "this marker is
    # supposed to be gone" vs "the marker should be here and is not". Without that line the next reader sees
    # "the fix build is missing something". Format per entry:  name|reason|since  (semicolon-separated).
    [string]$RetiredNeedles = 'ServerIconSource|removed-by-crash-fix(build 14C51D30D6A8 @02:52:13)|2026-09-12 02:51',
    [switch]$Preflight,
    [switch]$Confirmed,
    # captain ruling (B) 2026-09-12: tolerate instances whose exe path DIFFERS from the target (attribution is
    # by target pid + path + start time). Default stays the old caliber. Same-path instances are never
    # tolerated. Nothing is ever killed by this switch.
    [switch]$AllowForeignInstances,
    # (A) is kept: two instance samples this many seconds apart on the standard path.
    [int]$ForeignSampleSeconds = 5,
    [switch]$KeepRunning,
    # Zero-side-effect self-test of the verdict/accounting mapping (captain 2026-09-12 ruling): exercises every
    # (verdict x targetForeground) combination and the pass/inconclusive/fail columns WITHOUT starting a window,
    # so the new INCONCLUSIVE branch has runnable evidence that does not cost anyone a capture window.
    [switch]$SelfTestOutcome,
    # t179 SELF-TEST OF THE LEAK CHECK ITSELF. WHY: the leak check decides FAIL/exit for the whole run, so it
    # must be controllable in BOTH directions without waiting for a real flyout capture (its false positive was
    # only ever observed on the success path). This switch starts a stand-in process that carries the SAME
    # process name and lives in a private directory, exercises the real check against it, and then kills it --
    # no app window, no instance gate, no real data root. Exits before the -Exe/-SandboxRoot guards (like
    # -SelfTestOutcome). It NEVER relaxes the check: the "our process is still alive" case must still FAIL.
    [switch]$SelfTestLeak,
    [string]$SelfTestLeakDir = ''
)

$ErrorActionPreference = 'Continue'
$Tool = Join-Path $PSScriptRoot 'popup-capture.ps1'

function Say([string]$s) { Write-Output $s }

# ---------------------------------------------------------------------------------------------
# VERDICT / ACCOUNTING MAPPING (captain 2026-09-12 ruling on tolerant mode). Kept as a PURE function so it can
# be exercised without a window (`-SelfTestOutcome`) and so "the environment precondition was not met" can never
# silently become a failure. The captain's three clauses are encoded literally:
#   NON-BLANK     + targetForeground=True   => pass          (the ONLY shape allowed to claim a usable image)
#   NON-BLANK     + targetForeground=False  => inconclusive  (a NON-BLANK claim MUST carry targetForeground)
#   BLANK/SUSPECT + targetForeground=True   => fail          (precondition satisfied, image still blank)
#   BLANK/SUSPECT + targetForeground=False  => inconclusive  (env precondition unmet: not a defect, not a pass)
#   NO-PNG (no file / capturer died)        => fail          (not attributable to the precondition)
# RULE (unchanged): the exit code is NOT the verdict -- read `SUMMARY|`.
# ---------------------------------------------------------------------------------------------
function Get-CaptureOutcome([string]$verdict, [bool]$targetForeground) {
    if ($verdict -eq 'NON-BLANK') {
        if ($targetForeground) { return @{ Outcome = 'pass'; Reason = 'nonblank-with-foreground' } }
        return @{ Outcome = 'inconclusive'; Reason = 'nonblank-without-foreground' }
    }
    if ($verdict -eq 'BLANK/SUSPECT') {
        if ($targetForeground) { return @{ Outcome = 'fail'; Reason = 'blank-with-foreground-satisfied' } }
        return @{ Outcome = 'inconclusive'; Reason = 'target-not-foreground' }
    }
    return @{ Outcome = 'fail'; Reason = 'no-png' }
}

# ---------------------------------------------------------------------------------------------
# MENU-ITEMS reading (captain 2026-09-12 requirement). The captain's point: the SAME channel produced a
# 164x466 image (11 menu items) and a 164x378 image, so a popup image's identity needs more than sha12 --
# WIDTH/HEIGHT + distinctColors + the foreground flag -- and the real variable may be the NUMBER OF ITEMS.
# The app already logs that number itself (`MENU-INVENTORY ... total=N on=M`, ServerRowContextMenu.cs:262), so
# this is a READING rather than an inference from image height. Absence is reported, never defaulted.
# ---------------------------------------------------------------------------------------------
function Get-MenuItemsReading([string]$exePath) {
    $exeDir = Split-Path -Parent $exePath
    $log = Join-Path $exeDir 'shell-startup.log'
    if (-not (Test-Path -LiteralPath $log)) { return @{ Ok = $false; Log = $log; Reason = 'log-missing' } }
    $li = Get-Item -LiteralPath $log
    $logSha = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.Substring(0, 12)
    $lines = [System.IO.File]::ReadAllLines($log)
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        if ($lines[$i] -match 'MENU-INVENTORY') {
            $total = '<none>'; $on = '<none>'
            if ($lines[$i] -match 'total=(\d+)') { $total = $Matches[1] }
            if ($lines[$i] -match 'on=(\d+)') { $on = $Matches[1] }
            return @{ Ok = $true; Log = $log; Line = ($i + 1); Total = $total; On = $on
                LogBytes = $li.Length; LogMtime = $li.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'); LogSha12 = $logSha }
        }
    }
    return @{ Ok = $false; Log = $log; Reason = 'no MENU-INVENTORY line'
        LogBytes = $li.Length; LogMtime = $li.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'); LogSha12 = $logSha }
}

# TOOL-START anchor (captain 2026-09-12, same reasoning as the gate's GATE-START): a script can die BEFORE its
# first substantive line (ExecutionPolicy blocks .ps1 on this host, and there is no `pwsh` here), in which case
# the caller's exit code is NOT a verdict. RULE: no TOOL-START -- or no GATES|/VERIFY| anchor below -- means the
# run did not happen; never read the exit code alone.
Say ("TOOL-START|script=" + (Split-Path -Leaf $MyInvocation.MyCommand.Path) + "|ps=" + $PSVersionTable.PSVersion.ToString() + "|time=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|exe=" + $Exe + "|mode=" + $Mode)

# ---------------------------------------------------------------------------------------------
# -SelfTestOutcome: exit BEFORE any instance/preflight work. This is the runnable evidence for the
# INCONCLUSIVE branch (captain 2026-09-12) -- it starts no window, touches no instance and needs no freeze.
# ---------------------------------------------------------------------------------------------
if ($SelfTestOutcome) {
    $cases = @(
        @{ v = 'NON-BLANK';     fg = $true;  want = 'pass';         why = 'usable image' },
        @{ v = 'NON-BLANK';     fg = $false; want = 'inconclusive'; why = 'non-blank claim without the precondition' },
        @{ v = 'BLANK/SUSPECT'; fg = $true;  want = 'fail';         why = 'precondition met, still blank' },
        @{ v = 'BLANK/SUSPECT'; fg = $false; want = 'inconclusive'; why = 'measured 03:50:56 / 03:28:43 pair' },
        @{ v = 'NO-PNG';        fg = $true;  want = 'fail';         why = 'capturer produced nothing' },
        @{ v = 'NO-PNG';        fg = $false; want = 'fail';         why = 'no file => not excused by precondition' }
    )
    Say ('SELFTEST|mode=outcome|cases=' + $cases.Count + '|at=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
    $stPass = 0; $stFail = 0
    foreach ($c in $cases) {
        $r = Get-CaptureOutcome $c.v ([bool]$c.fg)
        $ok = ($r.Outcome -eq $c.want)
        if ($ok) { $stPass++ } else { $stFail++ }
        Say ('OUTCOME-CASE|verdict=' + $c.v + '|targetForeground=' + $c.fg + '|outcome=' + $r.Outcome + '|reason=' + $r.Reason + '|want=' + $c.want + '|ok=' + $ok + '|why=' + $c.why)
    }
    Say ('SELF-CHECK|cases=' + $cases.Count + '|pass=' + $stPass + '|fail=' + $stFail + '|sum=' + ($stPass + $stFail) + '|equal=' + (($stPass + $stFail) -eq $cases.Count))
    $stVerdict = 'FAIL'; if ($stFail -eq 0) { $stVerdict = 'PASS' }
    Say ('SUMMARY|selftest=outcome|cases=' + $cases.Count + '|pass=' + $stPass + '|fail=' + $stFail + '|inconclusive=' + @($cases | Where-Object { (Get-CaptureOutcome $_.v ([bool]$_.fg)).Outcome -eq 'inconclusive' }).Count + '|verdict=' + $stVerdict)
    if ($stFail -eq 0) { exit 0 }
    exit 1
}

# ---------------------------------------------------------------------------------------------
# t179 LEAK CHECK: pure identity predicate + bounded re-scan. WHY IT WAS REWRITTEN (measured, not theorised):
# the old check declared FAIL from ONE instantaneous sample keyed on pid equality, with an exe-path string
# compare done only inside the FAIL branch. Its recorded behaviour (t151 evidence, both arms): the tool printed
# `POST-STOP|mine=1` + `FAIL|OUR OWN shell instance survived the stop` and ended EXIT=1, while an operator
# re-scan 1-2 s later found ZERO instances of the target path (only a foreign one). Two ingredients were
# missing, and BOTH are already used by the very same file for window attribution (the APPPID discovery loop):
#   1. the START TIME of the candidate -- a recycled pid running another instance of the same exe path passes
#      "pid equal" AND "path equal" but never passes "started at the same instant as our own process";
#   2. a BOUNDED re-scan -- "was there a process at the first sample" is not "our process survived"; a
#      candidate that is gone inside the re-scan window is reported as transient and does NOT fail the run.
# The predicate is PURE (plain records) so both directions can be controlled without starting a window.
# ---------------------------------------------------------------------------------------------
function Get-LeakTicksOf($proc) {
    if ($null -eq $proc) { return [int64]0 }
    try { return [int64]$proc.StartTime.Ticks } catch { return [int64]0 }
}
function Format-Ticks([int64]$ticks) {
    if ($ticks -le 0) { return '<unreadable>' }
    try { return ([datetime]::new($ticks)).ToString('yyyy-MM-dd HH:mm:ss.fff') } catch { return '<unreadable>' }
}
# PURE predicate: is this candidate record the process THIS run started?
#   record fields: Id, Path, StartTicks.
function Test-LeakCandidateIsOurs($cand, [int]$targetPid, [string]$targetPath, [int64]$ourStartTicks) {
    if ($null -eq $cand) { return $false }
    if ([int]$cand.Id -ne $targetPid) { return $false }
    if ([string]$cand.Path -ne $targetPath) { return $false }
    # No reference time (or an unreadable candidate start time) => fall back to the stricter old answer: a
    # same-pid/same-path process is treated as ours. Never silently relaxed.
    if ($ourStartTicks -le 0) { return $true }
    if ([int64]$cand.StartTicks -le 0) { return $true }
    return ([int64]$cand.StartTicks -eq $ourStartTicks)
}
# Real-process scan: sample, classify, and repeat inside a bounded window. Returns a hashtable.
function Get-LeakScan([string]$targetName, [int]$targetPid, [string]$targetPath, [int64]$ourStartTicks, [int]$reScanMs, [int]$stepMs) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $samples = New-Object System.Collections.ArrayList
    $othersDetail = ''
    $othersCount = 0
    $mineSeenPath = ''
    $mineSeenTicks = [int64]0
    $mineSeenOurs = $false
    $lastMine = 0
    $lastOurs = $false
    while ($true) {
        $snap = @(Get-Process -Name $targetName -ErrorAction SilentlyContinue)
        $mine = @(); $others = @()
        foreach ($q in $snap) {
            $qp = ''
            try { $qp = [string]$q.Path } catch { $qp = '' }
            $qt = Get-LeakTicksOf $q
            $rec = New-Object psobject -Property @{ Id = [int]$q.Id; Path = $qp; StartTicks = [int64]$qt; Proc = $q }
            if ([int]$q.Id -eq $targetPid) { $mine += $rec } else { $others += $rec }
        }
        if ($othersDetail -eq '' -and $others.Count -gt 0) {
            $othersCount = $others.Count
            $othersDetail = (@($others) | ForEach-Object { [string]$_.Id + ':' + $_.Path }) -join ';'
        }
        $isOurs = $false
        if ($mine.Count -gt 0) {
            $isOurs = Test-LeakCandidateIsOurs $mine[0] $targetPid $targetPath $ourStartTicks
            if ($mineSeenPath -eq '') { $mineSeenPath = [string]$mine[0].Path; $mineSeenTicks = [int64]$mine[0].StartTicks }
            if ($isOurs) { $mineSeenOurs = $true }
        }
        $t = [int]$sw.Elapsed.TotalMilliseconds
        [void]$samples.Add(@{ T = $t; Mine = $mine.Count; Others = $others.Count; IsOurs = $isOurs })
        Say ("RESCAN|t=+" + $t + "ms|mine=" + $mine.Count + "|others=" + $others.Count + "|mineIsOurs=" + $isOurs + "|minePath=" + $mineSeenPath + "|mineStarted=" + (Format-Ticks $mineSeenTicks))
        $lastMine = $mine.Count
        $lastOurs = $isOurs
        if ($mine.Count -eq 0) { break }
        if ($t -ge $reScanMs) { break }
        Start-Sleep -Milliseconds $stepMs
    }
    $sw.Stop()
    $firstMine = [int]$samples[0].Mine
    return @{
        Samples = $samples
        SamplesN = $samples.Count
        FirstMine = $firstMine
        MineLeft = $lastMine
        Others = $othersCount
        OthersDetail = $othersDetail
        IsOurs = $mineSeenOurs
        Persisted = (($lastMine -gt 0) -and $lastOurs)
        Resolved = (($firstMine -gt 0) -and ($lastMine -eq 0))
        MinePath = $mineSeenPath
        MineTicks = $mineSeenTicks
        ElapsedMs = [int]$sw.Elapsed.TotalMilliseconds
    }
}

# ---------------------------------------------------------------------------------------------
# -SelfTestLeak: control the leak check in both directions. The stand-in is a copy of cmd.exe placed in a
# PRIVATE directory under the tool's own stand-in root and named AIPlayer.Shell.exe, so it carries exactly the
# process name the leak check filters on and a path that belongs to no other agent. It is started hidden
# (CreateNoWindow, stdin redirected so `/k` never returns) and is killed again before this block exits.
# ---------------------------------------------------------------------------------------------
if ($SelfTestLeak) {
    $stDir = $SelfTestLeakDir
    if ([string]::IsNullOrWhiteSpace($stDir)) { $stDir = Join-Path $env:TEMP 't179-leakctl' }
    New-Item -ItemType Directory -Force -Path $stDir | Out-Null
    $standIn = Join-Path $stDir 'AIPlayer.Shell.exe'
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32\cmd.exe') -Destination $standIn -Force
    $stName = 'AIPlayer.Shell'
    $stCases = 0; $stOk = 0; $stBad = 0
    Say ('SELFTEST|mode=leak|at=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + '|standIn=' + $standIn + '|standInBytes=' + (Get-Item -LiteralPath $standIn).Length + '|name=' + $stName)
    # CASE A (POSITIVE CONTROL): a process THIS block started and deliberately did NOT stop must still FAIL.
    $psi2 = New-Object System.Diagnostics.ProcessStartInfo
    $psi2.FileName = $standIn
    $psi2.Arguments = '/k'
    $psi2.UseShellExecute = $false
    $psi2.CreateNoWindow = $true
    $psi2.RedirectStandardInput = $true
    $psi2.RedirectStandardOutput = $true
    $psi2.RedirectStandardError = $true
    $sp = [System.Diagnostics.Process]::Start($psi2)
    $spTicks = Get-LeakTicksOf $sp
    Say ('SELFTEST-CASE-A|started=pid=' + $sp.Id + '|startTicks=' + $spTicks + '|started=' + (Format-Ticks $spTicks) + '|scenario=our own instance deliberately left running (must still be a FAIL)')
    $rA = Get-LeakScan $stName $sp.Id $standIn $spTicks 400 100
    $stCases++
    $okA = ($rA.Persisted -and $rA.IsOurs -and ($rA.MineLeft -gt 0))
    if ($okA) { $stOk++ } else { $stBad++ }
    Say ('SELFTEST-CASE-A|result=persisted=' + $rA.Persisted + '|isOurs=' + $rA.IsOurs + '|mineLeft=' + $rA.MineLeft + '|elapsedMs=' + $rA.ElapsedMs + '|want=persisted=True|ok=' + $okA + '|note=this is the criterion that must NOT be relaxed: a surviving self-started instance still fails')
    if (-not $sp.HasExited) { Stop-Process -Id $sp.Id -Force -ErrorAction SilentlyContinue }
    $w = 0
    while (-not $sp.HasExited -and $w -lt 3000) { Start-Sleep -Milliseconds 100; $w += 100 }
    Say ('SELFTEST-CASE-A-CLEANUP|pid=' + $sp.Id + '|hasExited=' + $sp.HasExited + '|waitMs=' + $w)
    # CASE B (NEGATIVE CONTROL == the original false-positive scenario): the same process, now stopped, must
    # NOT be reported as ours any more -- the run must not be failed by the leak check.
    $rB = Get-LeakScan $stName $sp.Id $standIn $spTicks 1500 250
    $stCases++
    $okB = ((-not $rB.Persisted) -and ($rB.MineLeft -eq 0) -and ($rB.FirstMine -eq 0))
    if ($okB) { $stOk++ } else { $stBad++ }
    Say ('SELFTEST-CASE-B|result=persisted=' + $rB.Persisted + '|mineLeft=' + $rB.MineLeft + '|firstSampleMine=' + $rB.FirstMine + '|samples=' + $rB.SamplesN + '|elapsedMs=' + $rB.ElapsedMs + '|want=persisted=False|ok=' + $okB + '|note=the t151 shape: after the stop, no instance of ours must be claimed')
    # CASE C (PID-RECYCLE DISCRIMINATION, pure): the record a recycled pid produces -- same pid, same exe path,
    # but a start time that is NOT ours -- must be rejected. The old rule (pid + path) accepted it, which is
    # exactly how a false FAIL could be printed for a process that is not ours.
    $recycledPid = 4242
    $recycled = New-Object psobject -Property @{ Id = [int]$recycledPid; Path = $standIn; StartTicks = [int64]($spTicks + 6000000000) }
    $ours = New-Object psobject -Property @{ Id = [int]$recycledPid; Path = $standIn; StartTicks = [int64]$spTicks }
    $otherPathRec = New-Object psobject -Property @{ Id = [int]$recycledPid; Path = ($standIn + '.other'); StartTicks = [int64]$spTicks }
    $vRecycled = Test-LeakCandidateIsOurs $recycled $recycledPid $standIn $spTicks
    $vOurs = Test-LeakCandidateIsOurs $ours $recycledPid $standIn $spTicks
    $vOtherPath = Test-LeakCandidateIsOurs $otherPathRec $recycledPid $standIn $spTicks
    $stCases++
    $okC = ((-not $vRecycled) -and $vOurs -and (-not $vOtherPath))
    if ($okC) { $stOk++ } else { $stBad++ }
    Say ('SELFTEST-CASE-C|recycledPidSamePathLaterStart=ours:' + $vRecycled + '|samePidSamePathSameStart=ours:' + $vOurs + '|samePidOtherPath=ours:' + $vOtherPath + '|want=recycled=False,same=True,otherPath=False|ok=' + $okC + '|note=the discriminator the old rule lacked (start-time identity); the old rule answered True,True,False')
    # CASE D: the "unreadable reference time" fallback must NOT relax anything (it keeps the old, stricter answer).
    $stCases++
    $vNoRef = Test-LeakCandidateIsOurs $ours $recycledPid $standIn ([int64]0)
    $okD = ($vNoRef -eq $true)
    if ($okD) { $stOk++ } else { $stBad++ }
    Say ('SELFTEST-CASE-D|noReferenceStartTime=ours:' + $vNoRef + '|want=True|ok=' + $okD + '|note=when our own start time is unknown the check falls back to the OLD stricter answer instead of silently passing')
    Say ('SELF-CHECK|leakselftest=cases=' + $stCases + '|pass=' + $stOk + '|fail=' + $stBad + '|sum=' + ($stOk + $stBad) + '|equal=' + (($stOk + $stBad) -eq $stCases))
    $stVerdict2 = 'FAIL'; if ($stBad -eq 0) { $stVerdict2 = 'PASS' }
    Say ('SUMMARY|selftest=leak|cases=' + $stCases + '|pass=' + $stOk + '|fail=' + $stBad + '|verdict=' + $stVerdict2)
    if ($stBad -eq 0) { exit 0 }
    exit 1
}

# ---------------------------------------------------------------------------------------------
# t151 GUARDS (shell/tools/capture-shell-flyout.ps1 used to start a REAL shell against the user's REAL data
# root, and -Exe defaulted to a machine-private path). Both are refused HERE, explicitly, before any instance
# work or preflight: a refusal must be visible, never a silent fallback to the real root.
#   -SandboxRoot empty            => FAIL|SANDBOX-REQUIRED      (exit 2)
#   -SandboxRoot inside real root => FAIL|SANDBOX-IS-REAL-ROOT  (exit 2)
#   -Exe empty                    => FAIL|EXE-REQUIRED          (exit 2)
# -SelfTestOutcome still runs without either (it exits above, starts no window and writes nothing).
# ---------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($SandboxRoot)) {
    Say 'FAIL|SANDBOX-REQUIRED: pass -SandboxRoot <dir>. This tool starts a real shell; without its own data root the shell writes the user real %LOCALAPPDATA%\AIPlayer (t104 ruling). Refusing to run.'
    exit 2
}
$sandboxFull = [System.IO.Path]::GetFullPath($SandboxRoot)
$realRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'AIPlayer'))
$realTrim = $realRoot.TrimEnd('\')
$sandboxTrim = $sandboxFull.TrimEnd('\')
if (($sandboxTrim -ieq $realTrim) -or $sandboxTrim.StartsWith(($realTrim + '\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    Say ('FAIL|SANDBOX-IS-REAL-ROOT: -SandboxRoot ' + $sandboxFull + ' points inside the real data root ' + $realRoot + '; refusing to run (this is the tool-side guard, not a warning)')
    exit 2
}
if (-not (Test-Path -LiteralPath $sandboxFull)) { New-Item -ItemType Directory -Path $sandboxFull -Force | Out-Null }
Say ('SANDBOX|root=' + $sandboxFull + '|exists=' + (Test-Path -LiteralPath $sandboxFull) + '|realRoot=' + $realRoot + '|at=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
# SEED: the servers page needs servers.json or the row hook has nothing to open. This is a one-way READ of the
# real root into the sandbox; the real root is never written by this tool (proven by the caller's before/after
# identities in the evidence file).
$seedSrc = Join-Path $realRoot 'servers.json'
$seedDst = Join-Path $sandboxFull 'servers.json'
if ((-not (Test-Path -LiteralPath $seedDst)) -and (Test-Path -LiteralPath $seedSrc)) {
    Copy-Item -LiteralPath $seedSrc -Destination $seedDst -Force
    $seedFi = Get-Item -LiteralPath $seedDst
    Say ('SANDBOX-SEED|servers.json|bytes=' + $seedFi.Length + '|sha256_12=' + (Get-FileHash -LiteralPath $seedDst -Algorithm SHA256).Hash.Substring(0, 12) + '|source=' + $seedSrc + '|note=read-only copy from the real root INTO the sandbox')
}
else {
    Say ('SANDBOX-SEED|servers.json|skipped|dstExists=' + (Test-Path -LiteralPath $seedDst) + '|srcExists=' + (Test-Path -LiteralPath $seedSrc))
}
if ([string]::IsNullOrWhiteSpace($Exe)) {
    Say 'FAIL|EXE-REQUIRED: pass -Exe <path to AIPlayer.Shell.exe>. The machine-private default was removed in t151; refusing to guess which build to capture.'
    exit 2
}

# The nested calls below go through powershell.exe, and $Tool contains a SPACE ("E:\AI Player\..."). Without
# explicit quoting the child receives a truncated -File path and returns NOTHING -- which is exactly how the
# first real attempt died silently ("no flyout candidate", mainHwnd=0) while the window was actually up.
# .NET Framework has no ArgumentList (README 10.8), so build the argument string with quotes by hand.
function Get-ToolArgLine([string[]]$ToolArgs) {
    return ('-NoProfile -ExecutionPolicy Bypass -File "' + $Tool + '" ' + (($ToolArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '))
}
function Invoke-CaptureTool([string[]]$ToolArgs) {
    $argline = Get-ToolArgLine $ToolArgs
    $cpsi = New-Object System.Diagnostics.ProcessStartInfo
    $cpsi.FileName = (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe')
    $cpsi.Arguments = $argline
    $cpsi.UseShellExecute = $false
    $cpsi.RedirectStandardOutput = $true
    $cpsi.RedirectStandardError = $true
    $cp = [System.Diagnostics.Process]::Start($cpsi)
    $so = $cp.StandardOutput.ReadToEnd()
    $se = $cp.StandardError.ReadToEnd()
    $cp.WaitForExit()
    $all = @()
    if ($so -ne '') { $all += ($so -split "`r?`n") }
    if ($se -ne '') { $all += ($se -split "`r?`n") }
    return @{ Lines = @($all | Where-Object { $_ -ne '' }); Exit = $cp.ExitCode; ArgLine = $argline }
}

# ---------------------------------------------------------------------------------------------
# Byte-level string scan helpers (captain 2026-09-12 criterion 2). Reader must handle BOTH ASCII and
# UTF-16LE, and must report the raw offsets: a MISS is only evidence when both readings are -1.
# [System.Array]::IndexOf is used as the fast native pre-filter for the first needle byte, so the
# PowerShell-level loop only runs over candidate positions (measured: 3 needles x 2 encodings = 559 ms
# on a 1.5 MB dll, reproducing the previously recorded offsets 1245967 / 1245639).
# ---------------------------------------------------------------------------------------------
function Find-Bytes([byte[]]$hay, [byte[]]$needle) {
    if ($needle.Length -eq 0 -or $hay.Length -lt $needle.Length) { return -1 }
    $last = $hay.Length - $needle.Length
    $start = 0
    while ($start -le $last) {
        $i = [System.Array]::IndexOf($hay, $needle[0], $start)
        if ($i -lt 0 -or $i -gt $last) { return -1 }
        $j = 1
        while ($j -lt $needle.Length -and $hay[$i + $j] -eq $needle[$j]) { $j++ }
        if ($j -eq $needle.Length) { return $i }
        $start = $i + 1
    }
    return -1
}
function Get-DllStringScan([string]$path, [string]$needle) {
    $r = @{ Ascii = -1; Utf16 = -1 }
    if (-not (Test-Path -LiteralPath $path)) { return $r }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $r.Ascii = Find-Bytes $bytes ([System.Text.Encoding]::ASCII.GetBytes($needle))
    $r.Utf16 = Find-Bytes $bytes ([System.Text.Encoding]::Unicode.GetBytes($needle))
    return $r
}

if (-not (Test-Path -LiteralPath $Exe)) { Say ("USAGE-EXIT|exe not found: " + $Exe); exit 2 }
if (-not (Test-Path -LiteralPath $Tool)) { Say ("USAGE-EXIT|tool not found: " + $Tool); exit 2 }
if ($Mode -eq 'hook' -and $Out -eq '' -and -not $Preflight) { Say 'USAGE-EXIT|-Out is required in hook mode (not in -Preflight)'; exit 2 }

# HARD GATE (added after I started a window twice while only MEANING to test the refusal path):
# starting a GUI window is a side effect a caller must ask for on purpose. Without -Confirmed this script
# refuses instead of silently opening a window just because the instance count happened to be 0.
# It sits BEFORE the instance check on purpose: "explicit intent" is the first condition, and being first
# makes it testable at any time (the instance guard would otherwise mask it).
if (-not $Confirmed -and -not $Preflight) {
    Say 'REFUSED|process start requires -Confirmed (this script opens a real window; pass -Confirmed only when you intend to capture, or use -Preflight to inspect conditions)'
    exit 3
}

$fi = Get-Item -LiteralPath $Exe
Say ("EXE|path=" + $fi.FullName + "|bytes=" + $fi.Length + "|sha256_12=" + (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.Substring(0, 12) + "|mtime=" + $fi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))

# PROVENANCE (captain 2026-09-12): a capture from a WORKING-TREE build must say so, otherwise the image
# silently claims a clean-HEAD provenance it does not have. Record HEAD + the in-flight files right here.
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$head = ''
try { $head = ((& git -C $repoRoot rev-parse HEAD 2>$null) | Select-Object -First 1) } catch { $head = '<git failed>' }
$dirty = @()
try { $dirty = @(& git -C $repoRoot status --porcelain -- shell/App shell/Services 2>$null) } catch { $dirty = @('<git failed>') }
# BUGFIX (found by RUNNING -Preflight at 02:39:30, not by reading): this line used to Trim() FIRST and then
# Substring(3). git porcelain is "XY path" = 2 status chars + 1 space, so Trim() removed the leading status
# space and Substring(3) then ate the first character of the PATH itself -- the field printed
# "hell/App/App.xaml.cs" instead of "shell/App/App.xaml.cs". The COUNT was right, so the wrong names went
# unnoticed: a field whose numbers are right and whose names are wrong still poisons a later attribution.
$dirtyNames = @($dirty | ForEach-Object { $t = [string]$_; if ($t.Length -gt 3) { $t.Substring(3).Trim() } else { $t.Trim() } })
# CODE IDENTITY (ui3, 2026-09-12): the .exe is a STABLE apphost stub -- it is byte-identical after a C#-only
# rebuild (measured: sha12 FFC52096BB5D with mtime 01:55:20 AND 01:59:25). The code lives in
# AIPlayer.Shell.dll. So the exe fields are only a LAUNCHER identity; the CODE identity is the dll's
# sha12+mtime. Independent structural proof: the app's own code strings exist in the dll and NOT in the exe
# (MENU-INVENTORY / SERVERS icon-missing => -1 in exe, present in dll).
$dllPath = [System.IO.Path]::ChangeExtension($fi.FullName, '.dll')
$dllSha = '<none>'; $dllMtime = '<none>'; $dllBytes = 0
if (Test-Path -LiteralPath $dllPath) {
    $dfi = Get-Item -LiteralPath $dllPath
    $dllSha = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.Substring(0, 12)
    $dllMtime = $dfi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
    $dllBytes = $dfi.Length
}
Say ("CODE-IDENTITY|codeDll=" + (Split-Path $dllPath -Leaf) + "|dllSha256_12=" + $dllSha + "|dllMtime=" + $dllMtime + "|dllBytes=" + $dllBytes + "|capturedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|head=" + $head + "|dirtyFilesRepo=" + $dirtyNames.Count + "|dirtyScope=shell/App+shell/Services" + "|note=THE CODE IDENTITY IS THE DLL; the exe fields below are LAUNCHER identity only; THE IMAGE BELONGS TO THIS DLL, NOT NECESSARILY TO THE CURRENT SOURCE (any capture is at least one edit behind). dirtyFilesRepo is REPO-scoped (shell/App + shell/Services): it is NOT comparable with a territory-scoped count (e.g. one screen's own files)")
$provLine = ("PROVENANCE|head=" + $head + "|exeSha256_12=" + (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.Substring(0, 12) + "|exeMtime=" + $fi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') + "|workingTreeDirtyFiles=" + $dirtyNames.Count + "|dirtyList=" + (($dirtyNames | Select-Object -First 12) -join ',') + "|dllSha256_12=" + $dllSha + "|dllMtime=" + $dllMtime + "|exeIsLauncherIdentity=true")
Say $provLine
# BUILDERS (captain 2026-09-12 ruling): a concurrent dotnet/MSBuild is NOT a blocker -- this script never builds
# and the target directory is not written by anybody else, so a build can only slow the run down, never
# contaminate the artifact. It is recorded as a SOFT warning so the reading carries the fact without turning
# it into a refusal. (instances >= 1 stays a HARD refusal: that one really does break popup attribution.)
$bDotnet = @(Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue).Count
$bMsbuild = @(Get-Process -Name 'MSBuild' -ErrorAction SilentlyContinue).Count
Say ("BUILDERS-SOFT|dotnet=" + $bDotnet + "|msbuild=" + $bMsbuild + "|blocks=false|capturedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|note=concurrent builds do not block this capture (we do not build; the target dir is not shared); recorded as a soft warning only")

# ---------------------------------------------------------------------------------------------
# CRITERION 3 (v3, captain 2026-09-12 ~02:43 -- REPLACES the pinned-mtime version): SOURCE GENERATION.
# The measured object is now the source GENERATION that produced this dll, identified by sha256_12, with
# mtime/bytes kept as accompanying readings. Why: mtime equality was both too strong and too weak -- it broke
# on any content-identical touch and it said nothing about content. v3 rule: sha equal => OK; sha differs =>
# STOP and report the difference (NOT a permanent refusal -- the captain decides between re-pinning the
# generation and re-capturing, judged by "does the change affect the server-menu shape").
# mtime drift alone does NOT stop the run: it is an observation (verdict=OK-WITH-MTIME-DRIFT).
# ---------------------------------------------------------------------------------------------
$syncFiles = @(
    @{ Rel = 'shell/App/Features/Servers/ServersPage.xaml.cs'; Expect = '2026-09-12 02:13:19.606'; Sha = $SyncCsSha; Bytes = 23741 },
    @{ Rel = 'shell/App/Features/Servers/ServersPage.xaml'; Expect = '2026-09-12 01:58:45.761'; Sha = $SyncXamlSha; Bytes = 4600 }
)
Say ("SOURCE-GEN-PIN|ServersPage.xaml.cs==sha256_12:" + $SyncCsSha + "|ServersPage.xaml==sha256_12:" + $SyncXamlSha + "|note=the pinned generation belongs to dll B156252C3771; re-pinning is a captain decision and takes effect via -SyncCsSha/-SyncXamlSha (no source edit)")
$syncBad = 0
$newestSrcMtime = [datetime]::MinValue
foreach ($sf in $syncFiles) {
    $abs = Join-Path $repoRoot $sf.Rel
    if (-not (Test-Path -LiteralPath $abs)) {
        Say ("SOURCE-SYNC|" + $sf.Rel + "|verdict=MISSING|expectedSha256_12=" + $sf.Sha + "|expectedMtime=" + $sf.Expect)
        $syncBad++
        continue
    }
    $sfi = Get-Item -LiteralPath $abs
    $got = $sfi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
    $sha = (Get-FileHash -LiteralPath $abs -Algorithm SHA256).Hash.Substring(0, 12)
    if ($sfi.LastWriteTime -gt $newestSrcMtime) { $newestSrcMtime = $sfi.LastWriteTime }
    $shaOk = ($sha -eq $sf.Sha)
    if (-not $shaOk) { $syncBad++ }
    $verdict = 'OK'
    if (-not $shaOk) { $verdict = 'MISMATCH' } elseif ($got -ne $sf.Expect) { $verdict = 'OK-WITH-MTIME-DRIFT' }
    Say ("SOURCE-SYNC|" + $sf.Rel + "|sha256_12=" + $sha + "|expectedSha256_12=" + $sf.Sha + "|verdict=" + $verdict + "|bytes=" + $sfi.Length + "|expectedBytes=" + $sf.Bytes + "|mtime=" + $got + "|expectedMtime=" + $sf.Expect + "|note=the CRITERION is the sha (source generation); mtime and bytes are observations, mtime drift alone does not stop the run")
}
# consistency observation (captain: "dllMtime >= ServersPage.* mtime"): an older dll is expected for a frozen
# capture target, but it must be VISIBLE rather than assumed away.
$dllOlder = '<unknown>'
if ($dllMtime -ne '<none>' -and $newestSrcMtime -ne [datetime]::MinValue) {
    $dllOlder = ([datetime]::ParseExact($dllMtime, 'yyyy-MM-dd HH:mm:ss.fff', $null) -lt $newestSrcMtime)
}
Say ("DLL-VS-SOURCE|dllMtime=" + $dllMtime + "|newestSourceMtime=" + $(if ($newestSrcMtime -eq [datetime]::MinValue) { '<none>' } else { $newestSrcMtime.ToString('yyyy-MM-dd HH:mm:ss.fff') }) + "|dllIsOlderThanSource=" + $dllOlder + "|note=observation only (a frozen target IS older); it is printed so 'the image belongs to an older dll' can never be read as 'the image is current'")

# ---------------------------------------------------------------------------------------------
# CRITERION 2: dual-encoding code scan on the SAME dll whose identity is printed above.
# The NEEDLE SET is a parameter, not a hard-coded pair: a marker string can DIE with the very generation
# it was chosen to identify -- measured 2026-09-12 03:03:52: `ServerIconSource` HITs the crash build
# (B156252C3771, utf16 @1245639) and MISSes ui3's fix build (14C51D30D6A8, ascii=-1 utf16=-1), because the
# fix removed exactly that XAML resource key. Choosing a marker must therefore ask "is it present in EVERY
# generation I intend to capture?", not "is it present now?" (VERIFY_S1 72). The default below is UNCHANGED
# behaviour; moving to stable markers (e.g. ServerRowContextMenu, which HITs both builds as ASCII) is a
# CAPTAIN decision and takes effect through -ScanNeedles, so no source edit is needed to re-pin markers.
# ---------------------------------------------------------------------------------------------
# BUGFIX (measured 03:05, first run of the parameterised version): the working variable MUST NOT be the
# parameter name. `[string]$ScanNeedles` carries a TYPE CONSTRAINT, and `$scanNeedles = @(...)` is the SAME
# variable (PowerShell names are case-insensitive) -- so the split array was coerced back into ONE string,
# joined by spaces: count=1, needle="A B", always MISS. The gate silently refused every build and looked
# like a data problem. Same family as every other finding here: a field whose name is reused keeps a type
# that nobody re-checks. Verified by a standalone probe: `$x -split ';'` = 2 items, but assigning the result
# back into the [string]-typed $x collapses it to "A B".
$needleList = @($ScanNeedles -split ';' | Where-Object { $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })
Say ("CODE-SCAN-NEEDLES|set=" + ($needleList -join ' | ') + "|count=" + $needleList.Count + "|note=every needle must HIT; a marker that only exists in one generation will STOP the run on any other generation")
# Retired markers get their own rows (captain 2026-09-12): "MISS" alone does not say whether the marker is
# SUPPOSED to be gone. If a retired marker is back in the active set, say so loudly instead of scanning it.
foreach ($retired in @($RetiredNeedles -split ';' | Where-Object { $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })) {
    $rp = $retired -split '\|'
    $rname = $rp[0].Trim()
    $rreason = '<none>'; $rsince = '<none>'
    if ($rp.Count -gt 1) { $rreason = $rp[1].Trim() }
    if ($rp.Count -gt 2) { $rsince = $rp[2].Trim() }
    $nowActive = ($needleList -contains $rname)
    $rc = Get-DllStringScan $dllPath $rname
    Say ("NEEDLE-RETIRED|" + $rname + "|reason=" + $rreason + "|since=" + $rsince + "|dll-ascii=" + $rc.Ascii + "|dll-utf16=" + $rc.Utf16 + "|stillInActiveSet=" + $nowActive + "|note=its MISS is EXPECTED and must NOT be read as 'the marker is missing from this build'; if stillInActiveSet=true the active set and the retirement list contradict each other")
}
$scanMiss = 0
$scanSw = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($nd in $needleList) {
    $sc = Get-DllStringScan $dllPath $nd
    $hit = ($sc.Ascii -ge 0) -or ($sc.Utf16 -ge 0)
    if (-not $hit) { $scanMiss++ }
    $enc = 'none'
    if ($sc.Ascii -ge 0 -and $sc.Utf16 -ge 0) { $enc = 'ascii+utf16' } elseif ($sc.Ascii -ge 0) { $enc = 'ascii' } elseif ($sc.Utf16 -ge 0) { $enc = 'utf16' }
    Say ("CODE-SCAN|needle=" + $nd + "|dll=" + (Split-Path $dllPath -Leaf) + "|ascii=" + $sc.Ascii + "|utf16=" + $sc.Utf16 + "|verdict=" + $(if ($hit) { 'HIT' } else { 'MISS' }) + "|hitEncoding=" + $enc + "|note=a negative verdict needs BOTH readings to be -1")
}
$scanSw.Stop()
Say ("CODE-SCAN-SUMMARY|needles=" + $needleList.Count + "|miss=" + $scanMiss + "|elapsedMs=" + $scanSw.ElapsedMilliseconds + "|dllSha256_12=" + $dllSha + "|dllMtime=" + $dllMtime + "|dllBytes=" + $dllBytes)

$gateBad = @()
if ($syncBad -gt 0) { $gateBad += ("source-generation " + $syncBad + "/2 sha mismatch") }
if ($scanMiss -gt 0) { $gateBad += ("code-scan " + $scanMiss + "/" + $needleList.Count + " needle MISS") }
if ($gateBad.Count -gt 0) {
    Say ("STOP|" + ($gateBad -join ' + ') + "|the image could not be attributed to this build and source; report the difference instead of capturing")
    if ($Preflight) { Say 'STOP-NOTE|preflight never refuses (its contract is exit 0), so this is reported only; a REAL run would exit 3 here' }
    else { exit 3 }
} else {
    Say ("GATES|verdict=PASS|sourceSync=OK|codeScan=HIT|note=the dll carries the menu hook and its server-menu UI is the pinned source")
}

# PREFLIGHT: everything except starting the process. Use this when you only want to know "would it refuse,
# and what identity would it use" -- it never creates a window.
$envChild = @{ ArgLine = (Get-ToolArgLine @('-Pid', '0', '-List')); Lines = @() }
function Get-EnvCheck {
    $epsi = New-Object System.Diagnostics.ProcessStartInfo
    $epsi.FileName = (Join-Path $env:SystemRoot 'System32\cmd.exe')
    $epsi.Arguments = '/c echo MENUVAR=[%SHELL_SELFTEST_SERVERMENU%] PAGEVAR=[%SHELL_START_PAGE%]'
    $epsi.UseShellExecute = $false
    $epsi.RedirectStandardOutput = $true
    $epsi.EnvironmentVariables['SHELL_START_PAGE'] = $StartPage
    if ($Mode -eq 'hook') { $epsi.EnvironmentVariables['SHELL_SELFTEST_SERVERMENU'] = '1' }
    $ep = [System.Diagnostics.Process]::Start($epsi)
    $o = $ep.StandardOutput.ReadToEnd()
    $ep.WaitForExit()
    return $o.Trim()
}
Say ("ENVCHECK|" + (Get-EnvCheck) + "|note=proves the ProcessStartInfo env block reaches the child (mechanism, not the app)")
Say ("NESTED-TOOL|path=" + $Tool + "|argline=" + $envChild.ArgLine + "|lines=" + $envChild.Lines.Count + "|note=quoted by hand because the path contains a space")
if ($Preflight) {
    # captain 2026-09-12: the MENU ITEM COUNT is printed as its own reading (phase=preflight reads the log as it
    # stands; a real run re-reads it post-capture, where the last line belongs to THIS run).
    $miPre = Get-MenuItemsReading $fi.FullName
    if ($miPre.Ok) {
        Say ("MENU-ITEMS|phase=preflight|total=" + $miPre.Total + "|on=" + $miPre.On + "|logLine=" + $miPre.Line + "|log=" + $miPre.Log + "|logBytes=" + $miPre.LogBytes + "|logMtime=" + $miPre.LogMtime + "|logSha12=" + $miPre.LogSha12 + "|note=read from the app's own MENU-INVENTORY line, not inferred from image height")
    }
    else {
        Say ("MENU-ITEMS|phase=preflight|absent|reason=" + $miPre.Reason + "|log=" + $miPre.Log + "|logBytes=" + $miPre.LogBytes + "|note=absence is reported, never defaulted to a number")
    }
    Say 'PREFLIGHT|conditions above; no process was started'
    exit 0
}

# INSTANCE GATE (captain 2026-09-12, ruling (B) -- parameters -AllowForeignInstances / -ForeignSampleSeconds):
#   default (no switch) = the old caliber: any AIPlayer.Shell present => REFUSED.
#   (A) is kept as the standard path: sample TWICE, 5 s apart; a window that appears between the samples still
#     refuses (that is how the 03:25 race slipped through a single sample).
#   (B) `-AllowForeignInstances` tolerates ONLY instances whose exe path differs from the target: attribution
#     uses target pid + exe path + start time, so an unrelated window cannot be mistaken for ours. An instance
#     with the SAME exe path is NEVER tolerated (that is exactly the ambiguous case) => REFUSED|AMBIGUOUS.
#   Nothing is ever killed: this gate only decides whether WE start our own window.
$samePathPresent = @()
$foreignPresent = @()
for ($s = 1; $s -le 2; $s++) {
    $snap = @(Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue)
    $sameN = 0
    $foreignN = 0
    foreach ($q in $snap) {
        $qp = ''
        try { $qp = $q.Path } catch { $qp = '' }
        if ($qp -eq $fi.FullName) {
            $sameN++
            if (@($samePathPresent | Where-Object { $_.Id -eq $q.Id }).Count -eq 0) { $samePathPresent += $q }
        }
        else {
            $foreignN++
            if (@($foreignPresent | Where-Object { $_.Id -eq $q.Id }).Count -eq 0) { $foreignPresent += $q }
        }
    }
    Say ("INSTANCE-SAMPLE|" + $s + "/2|" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|instances=" + $snap.Count + "|samePathAsTarget=" + $sameN + "|foreign=" + $foreignN)
    if ($s -eq 1) { Start-Sleep -Seconds $ForeignSampleSeconds }
}
$sameIds = (@($samePathPresent) | ForEach-Object { [string]$_.Id }) -join ','
if (@($samePathPresent).Count -gt 0) {
    Say ("REFUSED|AMBIGUOUS: " + @($samePathPresent).Count + " instance(s) run the SAME exe as the target (" + $fi.FullName + ") -- pid=" + $sameIds + "; -AllowForeignInstances never tolerates this case")
    exit 3
}
if (@($foreignPresent).Count -gt 0) {
    if (-not $AllowForeignInstances) {
        Say ("REFUSED|another AIPlayer.Shell is already running (pid=" + (@($foreignPresent)[0].Id) + "); close it first so the popup can be attributed (or pass -AllowForeignInstances to tolerate DIFFERENT-path instances)")
        exit 3
    }
    $foreignDetail = ''
    foreach ($q in @($foreignPresent)) {
        $qp2 = ''
        try { $qp2 = $q.Path } catch { $qp2 = '' }
        if ($foreignDetail -ne '') { $foreignDetail += ';' }
        $foreignDetail += ([string]$q.Id + ':' + $qp2)
    }
    Say ("FOREIGN-INSTANCES-TOLERATED|count=" + @($foreignPresent).Count + "|others=[" + $foreignDetail + "]|note=tolerated because their exe path differs from the target and attribution uses target pid + exe path + start time; they are NEVER stopped by this run")
}


# .NET Framework ProcessStartInfo has NO ArgumentList: set Arguments as one quoted string (see README 10.8).
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $fi.FullName
$psi.Arguments = ''
$psi.UseShellExecute = $false
# WORKING DIRECTORY = the app's own folder. UNTESTED HYPOTHESIS for the 0xC000027B crashes of 02:45/02:47:
# with UseShellExecute=$false and no explicit WorkingDirectory the child inherits the CALLER's cwd (here the
# repo root), while every other agent starts the shell from its output folder -- if anything is resolved
# relative to the cwd (native libs / mpv assets), the two runs differ exactly in that. Setting it removes the
# variable; it does NOT by itself prove the crash was caused by it (that needs an A/B run).
$psi.WorkingDirectory = (Split-Path $fi.FullName -Parent)
$psi.EnvironmentVariables['SHELL_START_PAGE'] = $StartPage
if ($Mode -eq 'hook') { $psi.EnvironmentVariables['SHELL_SELFTEST_SERVERMENU'] = '1' }
else { [void]$psi.EnvironmentVariables.Remove('SHELL_SELFTEST_SERVERMENU') }
# t214 F1 (measured 2026-09-12): until now -SandboxRoot was SEEDED ONLY -- this variable was NEVER set, so the
# shell ran against the REAL data root (its own log line says `APPDATA-ROOT ... source=default`), every capture
# appended to the real root's logs\aiplayer.log, and the single-instance mutex was keyed to the REAL root, so a
# foreign instance made our launch fail with `SINGLE-INSTANCE-REJECTED`. Injecting the root makes the sandbox
# real: the shell writes <sandbox>\logs\aiplayer.log (exactly the path read at the `$log = Join-Path
# $sandboxFull 'logs\aiplayer.log'` line below) and the mutex is keyed to the sandbox root.
# PROOF OF EFFECT is not this line: it is the shell-side log's `APPDATA-ROOT ... source=override` line.
$psi.EnvironmentVariables['AIPLAYER_APPDATA_ROOT'] = $sandboxFull
Say ("SANDBOX-ENV|AIPLAYER_APPDATA_ROOT=" + $sandboxFull + "|note=the launched shell must report APPDATA-ROOT source=override for this root; without this line it silently used the real root (t214 F1)")

# t151: read the SANDBOX log, never the real root. The shell writes <AIPLAYER_APPDATA_ROOT>\logs\aiplayer.log,
# so this path is derived from the sandbox root injected below (the real root is only ever READ, for the seed
# copy above). The caller's before/after identities of the real root's protected files are the proof.
$log = Join-Path $sandboxFull 'logs\aiplayer.log'
$logSizeBefore = -1
$logOffsetLines = 0
if (Test-Path -LiteralPath $log) {
    $logSizeBefore = (Get-Item -LiteralPath $log).Length
    # the kernel/shell log is GLOBAL (shared with every other agent): remember where OUR lines start so a
    # diagnosis never attributes someone else's run to ours (services flagged this trap too).
    $logOffsetLines = @([System.IO.File]::ReadAllLines($log)).Count
}
$t0 = Get-Date
Say ("START|mode=" + $Mode + "|page=" + $StartPage + "|at=" + $t0.ToString('yyyy-MM-dd HH:mm:ss.fff') + "|logBytesBefore=" + $logSizeBefore + "|logLinesBefore=" + $logOffsetLines)
$p = [System.Diagnostics.Process]::Start($psi)
if ($null -eq $p) { Say 'FAIL|process did not start'; exit 1 }
Say ("PID|started=" + $p.Id)
# t179: OUR identity must be captured NOW, while the process exists (after it exits, Process.StartTime can be
# unreadable). The leak check below uses it to separate "our process survived" from "our pid was recycled".
$ourStartTicks = Get-LeakTicksOf $p
Say ("OUR-PID-IDENTITY|pid=" + $p.Id + "|startTicks=" + $ourStartTicks + "|startedAt=" + (Format-Ticks $ourStartTicks) + "|note=the leak check compares pid + exe path + THIS start time; a recycled pid running another instance of the same exe path cannot pass all three")

# PID DISCOVERY (measured 02:10: DIAG|list|ERROR|target pid not found: 19280 while the app WAS up and had
# logged "ServersPage loaded"): the pid returned by Process.Start is not necessarily the pid that owns the
# window (launcher/bootstrap re-exec, or the app's own restart path). So resolve the REAL owner by path +
# start time, and never trust the single pid -- the same lesson ui3 recorded for his own capture runs.
$appPid = 0
$discoverDeadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $discoverDeadline) {
    $cand = @(Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue | Where-Object {
            $pp = ''
            try { $pp = $_.Path } catch { $pp = '' }
            ($pp -eq $fi.FullName) -and ($_.StartTime -ge $t0.AddSeconds(-2))
        })
    if ($cand.Count -gt 0) { $appPid = ($cand | Sort-Object StartTime | Select-Object -First 1).Id; break }
    Start-Sleep -Milliseconds 500
}
if ($appPid -eq 0) {
    Say ("FAIL|no running process matches path " + $fi.FullName + " within 15s (started pid=" + $p.Id + ")")
    if (-not $KeepRunning) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    exit 1
}
Say ("APPPID|windowOwner=" + $appPid + "|startedPid=" + $p.Id + "|note=the window owner is the pid we sniff; Process.Start's pid may be a short-lived launcher")

$deadline = $t0.AddSeconds($TimeoutSec)
$mainHwnd = 0
$mainArea = 0
$pick = $null
$rawList = @()
$listCalls = 0
$procDied = $false
$procExitCode = '<not-exited>'
$procExitAt = '<n/a>'
$pidGoneAt = '<no>'
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 1200
    # LIVENESS FIRST (added after the 02:45:39 run): that run reported "no flyout candidate ... mainHwnd=0"
    # and its verdict blamed the candidate enumeration, while the truth was that the APP WAS GONE (the log's
    # last line was ARMED and popup-capture answered "target pid not found" from the very first call). Two
    # very different situations -- "the flyout was not among the windows" vs "there was no process left" --
    # produced the same sentence. Check the process before blaming the window list.
    if ($p.HasExited) {
        try { $procExitCode = $p.ExitCode } catch { $procExitCode = '<unreadable>' }
        $procExitAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
        $procDied = $true
        Say ("PROC|exited=true|exitCode=" + $procExitCode + "|at=" + $procExitAt + "|listCallsSoFar=" + $listCalls + "|note=the app exited BEFORE any window listing; anything below about 'candidates' is a consequence, not a cause")
        break
    }
    $listCalls++
    $res = Invoke-CaptureTool @('-Pid', [string]$appPid, '-List')
    $lines = @($res.Lines)
    $rawList = $lines
    $pidGoneAt = '<no>'
    if (@($lines | Where-Object { [string]$_ -match 'target pid not found' }).Count -gt 0) {
        $pidGoneAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
        # INSTRUMENT (2026-09-12 03:28): the lister claimed "target pid not found" while the parent could see
        # the process alive. A diagnostic run with the SAME invocation succeeded 6/6 times, so the failure is
        # not the invocation shape -- print the exact argline, the child's exit code, and an INDEPENDENT
        # Get-Process from the parent at that very moment, so the next reader gets a mechanism and not a guess.
        $indep = '<none>'
        try { $iq = Get-Process -Id $appPid -ErrorAction Stop; $indep = 'alive name=' + $iq.ProcessName + ' mwh=' + $iq.MainWindowHandle } catch { $indep = 'THROW:' + $_.Exception.Message }
        Say ("DIAG|listcall|call=" + $listCalls + "|argline=" + $res.ArgLine + "|exit=" + $res.Exit + "|indepGetProcess=" + $indep + "|appPid=" + $appPid + "|startedPid=" + $p.Id + "|parentHasExited=" + $p.HasExited)
    }
    $cands = @()
    foreach ($l in $lines) {
        $s = [string]$l
        if ($s -notlike 'CAND|*') { continue }
        $f = @{}
        foreach ($kv in ($s.Substring(5) -split '\|')) { $i = $kv.IndexOf('='); if ($i -gt 0) { $f[$kv.Substring(0, $i)] = $kv.Substring($i + 1) } }
        if (-not $f.ContainsKey('hwnd')) { continue }
        $cands += , @{ hwnd = [int]$f['hwnd']; cls = $f['class']; title = $f['title']; vis = $f['visible']; size = $f['size']; popup = $f['popup'] }
    }
    if ($cands.Count -eq 0) { continue }
    # the main window = the biggest visible one; remember it, never capture it as the flyout
    foreach ($c in $cands) {
        if ($c.vis -ne 'True' -or $c.size -notmatch '^(\d+)x(\d+)$') { continue }
        $a = [int]$Matches[1] * [int]$Matches[2]
        if ($a -gt $mainArea) { $mainArea = $a; $mainHwnd = $c.hwnd }
    }
    # flyout candidates: visible, non-empty, NOT the main window, smaller than half the main area.
    # BUGFIX: when the main window was not identified, mainArea stays 0 and "area < 0" could never be true --
    # that is how this harness once reported "no flyout candidate" while the window was actually on screen.
    $pool = @($cands | Where-Object {
            if ($_.vis -ne 'True') { return $false }
            if ($_.size -notmatch '^(\d+)x(\d+)$') { return $false }
            if ($_.hwnd -eq $mainHwnd) { return $false }
            $area = [int]$Matches[1] * [int]$Matches[2]
            if ($area -le 0) { return $false }
            if ($mainArea -gt 0 -and $area -ge ($mainArea / 2)) { return $false }
            return $true
        })
    if ($Mode -eq 'hook' -and $pool.Count -gt 0) {
        $withPopup = @($pool | Where-Object { $_.popup -eq 'True' })
        if ($withPopup.Count -gt 0) { $pick = $withPopup[0] } else { $pick = ($pool | Sort-Object { $s2 = $_.size -split 'x'; [int]$s2[0] * [int]$s2[1] })[0] }
        break
    }
}

if ($Mode -eq 'none') {
    # negative control: with no hook there must be NO menu popup and NO MENU-* log line
    # BUGFIX (02:47:44 run): `-Mode none` printed "PASS (no menu popup without the hook)" while the app had
    # ALREADY CRASHED (exitCode 0xC000027B) -- so "no popup" was being attributed to the absent hook when the
    # real reason was that nothing was alive to show one. A control may only PASS on a control that ran.
    $menuLines = 0
    if (Test-Path -LiteralPath $log) {
        $all = [System.IO.File]::ReadAllLines($log)
        # count only lines OUR run produced: the log is global (every agent writes to it), so a whole-file
        # count would credit other people's runs to this control.
        $menuLines = @($all | Select-Object -Skip ([Math]::Max(0, $logOffsetLines)) | Where-Object { $_ -match 'MENU-(SELFTEST|OPEN|INVENTORY)' }).Count
    }
    Say ("CONTROL|mode=none|popupCandidates=" + (@($pool).Count) + "|picked=" + ($(if ($pick) { $pick.hwnd } else { 'none' })) + "|menuLogLinesSinceStart=" + $menuLines + "|appExited=" + $procDied + "|exitCode=" + $procExitCode + "|exitAt=" + $procExitAt + "|observedSec=" + [int]((Get-Date) - $t0).TotalSeconds)
    if (-not $KeepRunning) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    if ($procDied) {
        Say ("CONTROL-RESULT|INCONCLUSIVE (the app exited with code " + $procExitCode + " after " + [int]((Get-Date) - $t0).TotalSeconds + "s -- 'no popup' cannot be attributed to the absent hook, because nothing was alive to show one)")
        exit 1
    }
    if ($null -eq $pick) { Say 'CONTROL-RESULT|PASS (no menu popup without the hook)'; exit 0 }
    Say 'CONTROL-RESULT|FAIL (a popup-looking window appeared without the hook -- attribution is not proven)'
    exit 1
}

if ($null -eq $pick) {
    # Diagnose, do not just fail: the first real attempt printed "no flyout candidate" while the window was
    # actually up, and nothing in the output said WHY. The log is the authority on what the app did.
    Say ("FAIL|no flyout candidate within " + $TimeoutSec + "s (mainHwnd=" + $mainHwnd + " mainArea=" + $mainArea + ")")
    Say ("PROC|exited=" + $procDied + "|exitCode=" + $procExitCode + "|exitAt=" + $procExitAt + "|pidGoneFirstSeenAt=" + $pidGoneAt + "|startedPid=" + $p.Id + "|appPid=" + $appPid)
    Say ("DIAG|listCalls=" + $listCalls + "|lastListLines=" + (@($rawList).Count))
    # captain 2026-09-12: print the child's RAW output, not a curated excerpt -- the earlier diagnosis only
    # showed 8 lines and the "ERROR|target pid not found" text came from the CHILD, which made it look like a
    # quoting/argument problem. Everything the child wrote (stdout+stderr merged) is dumped here.
    foreach ($l in @($rawList)) { Say ("DIAG|list|" + [string]$l) }
    $win = @()
    if (Test-Path -LiteralPath $log) {
        $all = [System.IO.File]::ReadAllLines($log)
        $win = @($all | Select-Object -Skip ([Math]::Max(0, $logOffsetLines)) | Where-Object { $_ -match 'Nav ->|ServersPage|MENU-|MainWindow|SHELL-SELFTEST|SHELL_START_PAGE|Unhandled|EXCEPTION' })
    }
    Say ("DIAG|logLinesSinceStart=" + $win.Count)
    foreach ($l in ($win | Select-Object -First 10)) { Say ("DIAG|log|" + [string]$l) }
    $hadNav = @($win | Where-Object { $_ -match 'Nav -> ' + $StartPage }).Count -gt 0
    $hadPage = @($win | Where-Object { $_ -match 'ServersPage loaded' }).Count -gt 0
    # BUGFIX (02:45:39 run): the old predicate was `-match 'MENU-'`, which is true for `MENU-SELFTEST ARMED`
    # -- i.e. the hook was QUEUED. It then printed "hook ran; the flyout HWND was simply not among the
    # enumerated candidates", which sent the reader looking at window enumeration while the log's last line
    # proved the hook never reached BEGIN. ARMED / BEGIN / layout-FAIL are three different states and each
    # must be counted separately (same family as catch-shape vs finding, exists vs is-the-file-it-claims).
    $hadArmed = @($win | Where-Object { $_ -match 'MENU-SELFTEST ARMED' }).Count -gt 0
    $hadBegin = @($win | Where-Object { $_ -match 'MENU-SELFTEST BEGIN' }).Count -gt 0
    $hadLayoutFail = @($win | Where-Object { $_ -match 'MENU-SELFTEST FAIL' }).Count -gt 0
    $verdict = 'the app never reached the servers page'
    if ($hadPage -and -not $hadArmed) { $verdict = 'page loaded but NO ARMED line: the hook env var did not reach the app (a menu that never opened leaves no log line at all when the variable is unset)' }
    elseif ($hadArmed -and -not $hadBegin -and -not $hadLayoutFail) { $verdict = 'hook ARMED but never BEGAN and never hit the 60-layout FAIL: the process most likely exited inside the wait (see the PROC| line) or the UI thread stopped pumping -- this is NOT a window-enumeration problem' }
    elseif ($hadArmed -and $hadLayoutFail -and -not $hadBegin) { $verdict = 'hook ARMED then failed after 60 layout passes (first row container never materialised): the menu was never shown' }
    elseif ($hadBegin) { $verdict = 'hook reached BEGIN: the flyout was shown and the HWND was simply not among the enumerated candidates' }
    Say ("DIAG|verdict|reachedStartPage=" + $hadNav + "|serversPageLoaded=" + $hadPage + "|menuHookArmed=" + $hadArmed + "|menuHookBegan=" + $hadBegin + "|menuLayoutFail=" + $hadLayoutFail + "|=> " + $verdict)
    if (-not $KeepRunning) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Say ("STOPPED|pid=" + $p.Id) }
    # t214 F2 (measured 2026-09-12): this FAILURE exit used to return right here, so a run that never reached a
    # flyout left NO machine-readable statement about the instances it started -- while the success path has one
    # (POST-STOP|/leakFail). The invariant below runs the same bounded re-scan as the success path, so "this run
    # left no instance behind" is readable on the failure path too; it NEVER upgrades the verdict (the exit code
    # of this branch is already 1) and it never claims anything about foreign instances.
    $failScan = Get-LeakScan 'AIPlayer.Shell' ([int]$p.Id) $fi.FullName $ourStartTicks 2000 250
    Say ("LEAK-INVARIANT|flow=no-flyout|ourPid=" + $p.Id + "|mine=" + $failScan.MineLeft + "|others=" + $failScan.Others + "|isOurs=" + $failScan.IsOurs + "|persisted=" + $failScan.Persisted + "|resolved=" + $failScan.Resolved + "|windowMs=" + $failScan.ElapsedMs + "|leftNoInstance=" + (($failScan.MineLeft -eq 0) -or $failScan.Resolved))
    if ($failScan.Persisted -and $failScan.IsOurs) { Say 'POST-FAIL-NOTE|our own instance is still alive after this failed run -- kill it by hand before anyone else runs' }
    exit 1
}

if ($Sidecar -eq '') { $Sidecar = [System.IO.Path]::ChangeExtension($Out, '.txt') }

# G5 posture (the official fix for the refusal ui3 hit: "foreground moved to a window of the TARGET process").
# The guard refuses ONLY when the foreground MOVES TO a target-pid window inside its observation window
# (entry -> after capture). A freshly started GUI process often activates itself, so the prescribed action is
# to WAIT FOR THE FOREGROUND TO SETTLE before invoking the capture -- then entry-foreground equals
# after-foreground (both on the target, or both on whatever else holds it) and no move is attributed to us.
# This block only READS the foreground; it never calls SetForegroundWindow/SetWindowPos/ShowWindow.
Add-Type -Namespace PopCapFg -Name Fg -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
'@ -ErrorAction SilentlyContinue
function Get-FgPair {
    $h = [PopCapFg.Fg]::GetForegroundWindow()
    $fpid = 0
    [void][PopCapFg.Fg]::GetWindowThreadProcessId($h, [ref]$fpid)
    return @{ Hwnd = [int64]$h; Pid = $fpid }
}
$settled = $false
for ($k = 0; $k -lt 12; $k++) {
    $a = Get-FgPair
    Start-Sleep -Milliseconds 700
    $b = Get-FgPair
    Say ("FG|sample=" + $k + "|aHwnd=" + $a.Hwnd + "|aPid=" + $a.Pid + "|bHwnd=" + $b.Hwnd + "|bPid=" + $b.Pid + "|stable=" + ($a.Hwnd -eq $b.Hwnd))
    if ($a.Hwnd -eq $b.Hwnd) { $settled = $true; break }
}
$fgNow = Get-FgPair
Say ("FG-SETTLED|" + $settled + "|foregroundPid=" + $fgNow.Pid + "|targetPid=" + $appPid + "|isTargetForeground=" + ($fgNow.Pid -eq $appPid) + "|note=G5 refuses only when the foreground MOVES TO the target pid during the capture; waiting for stability here is the prescribed fix (README 10.9)")
# CAPTURE PRECONDITION (measured 03:50:56, control 1 of ruling B): with the foreground on a FOREIGN pid the
# WinUI popup came back BLANK/SUSPECT on BOTH attempts (164x466, distinctColors=18, identical bytes), while the
# successful 03:28:43 run had isTargetForeground=True (164x378, distinctColors=162). Attribution was correct in
# both cases -- this is a capture-QUALITY precondition, so it is printed as a reading, not turned into a gate.
$targetFg = ($fgNow.Pid -eq $appPid)
if (-not $targetFg) {
    Say ("CAPTURE-PRECONDITION|targetForeground=False|foregroundPid=" + $fgNow.Pid + "|targetPid=" + $appPid + "|note=measured 03:50:56: a WinUI popup whose window is NOT foreground can come back BLANK/SUSPECT even through the printwindow channel; the image is still attributed to the right pid, but expect a blank. captain 2026-09-12: this case is INCONCLUSIVE, never a FAIL, and we never bring the target window to the foreground")
}
else {
    Say ("CAPTURE-PRECONDITION|targetForeground=True|foregroundPid=" + $fgNow.Pid + "|targetPid=" + $appPid + "|note=the precondition for a NON-BLANK claim is satisfied (captain 2026-09-12: a non-blank reading must carry this line)")
}

$classGlob = $pick.cls + '*'
$capText = ''
$capExit = 1
$verdict = 'NO-PNG'
$detail = ''
$pngBytes = 0; $pngSha = ''
$capW = 0; $capH = 0; $capColors = 0
$attUsed = 0; $attNonBlank = 0; $attBlank = 0; $attNoPng = 0
for ($att = 1; $att -le 2; $att++) {
    $cap = Invoke-CaptureTool @('-Pid', [string]$appPid, '-Hwnd', [string]$pick.hwnd, '-ExpectClassLike', $classGlob, '-Out', $Out, '-Sidecar', $Sidecar)
    $capText = (($cap.Lines) | ForEach-Object { [string]$_ }) -join "`n"
    $capExit = $cap.Exit
    Say ("CAPTURE-ATTEMPT|" + $att + "/2|capExit=" + $capExit + "|at=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
    Say $capText
    # independent verification: never trust the capturer's own non-blank verdict
    $verdict = 'NO-PNG'
    $detail = ''
    if (Test-Path -LiteralPath $Out) {
        Add-Type -AssemblyName System.Drawing
        $bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Out).Path)
        $colors = New-Object 'System.Collections.Generic.HashSet[string]'
        $min = 255; $max = 0; $cnt = 0
        for ($y = 0; $y -lt $bmp.Height; $y += 7) {
            for ($x = 0; $x -lt $bmp.Width; $x += 7) {
                $c = $bmp.GetPixel($x, $y)
                $l = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
                if ($l -lt $min) { $min = $l }
                if ($l -gt $max) { $max = $l }
                $cnt++
                [void]$colors.Add("$($c.R),$($c.G),$($c.B)")
            }
        }
        $detail = "dim=$($bmp.Width)x$($bmp.Height) sampled=$cnt distinctColors=$($colors.Count) lumMin=$min lumMax=$max"
        $capW = $bmp.Width; $capH = $bmp.Height; $capColors = $colors.Count
        $verdict = 'BLANK/SUSPECT'
        if ($colors.Count -gt 20 -and ($max - $min) -gt 40) { $verdict = 'NON-BLANK' }
        $bmp.Dispose()
    }
    if (Test-Path -LiteralPath $Out) { $pngBytes = (Get-Item -LiteralPath $Out).Length; $pngSha = (Get-FileHash -LiteralPath $Out -Algorithm SHA256).Hash.Substring(0, 12) }
    Say ("VERIFY|independent|" + $verdict + "|" + $detail + "|png=" + $Out + "|bytes=" + $pngBytes + "|sha256_12=" + $pngSha + "|capExit=" + $capExit + "|attempt=" + $att)
    $attUsed = $att
    if ($verdict -eq 'NON-BLANK') { $attNonBlank++ } elseif ($verdict -eq 'BLANK/SUSPECT') { $attBlank++ } else { $attNoPng++ }
    if ($verdict -eq 'NON-BLANK') { break }
    # MEASURED 03:49:48: a flyout that is STILL EXPANDING (captured at 164x466 while the settled menu is
    # 164x378) came back with only 18 distinct colours -- i.e. a genuine BLANK/SUSPECT. One settle + one retry,
    # each reported as its own attempt, keeps the verdict honest without turning a timing artifact into
    # "this tool cannot capture".
    if ($att -lt 2) { Say ("CAPTURE-RETRY|attempt=" + $att + "|reason=verdict=" + $verdict + "|waitMs=2000"); Start-Sleep -Milliseconds 2000 }
}

# captain 2026-09-12 ruling on tolerant mode: an UNMET capture precondition (target window not foreground)
# makes this run's imaging result INCONCLUSIVE -- neither a defect (fail) nor a usable image (pass), so it is
# counted in its own column and never folded into `fail`. The one backoff retry above stays: we only re-shoot,
# we never relax a criterion, and we NEVER bring the target window to the foreground.
$oc = Get-CaptureOutcome $verdict $targetFg
$outcome = $oc.Outcome; $outcomeReason = $oc.Reason
if ($outcome -eq 'inconclusive') {
    Say ("INCONCLUSIVE|reason=" + $outcomeReason + "|verdict=" + $verdict + "|targetForeground=" + $targetFg + "|attempts=" + $attUsed + "|at=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|note=the environment precondition was not met, so this run proves nothing about the UI; it is NOT a failure and NOT a pass")
}
if (($verdict -eq 'NON-BLANK') -and (-not $targetFg)) {
    Say ('NON-BLANK-CLAIM-VOID|reason=target-not-foreground|note=captain 2026-09-12: a reading that claims a non-blank image MUST also carry targetForeground=True; without it the claim does not stand (the file is still reported, its verdict is not)')
}

# MENU ITEM COUNT for THIS run (captain 2026-09-12): the app logs its own inventory when the flyout opens, so the
# last MENU-INVENTORY line belongs to this run -- the item count becomes a reading attached to the image instead
# of a hypothesis about "why the same channel sometimes returns a blank".
$miPost = Get-MenuItemsReading $fi.FullName
$miTotal = 'absent'; $miOn = 'absent'
if ($miPost.Ok) {
    $miTotal = $miPost.Total; $miOn = $miPost.On
    Say ("MENU-ITEMS|phase=post-capture|total=" + $miTotal + "|on=" + $miOn + "|logLine=" + $miPost.Line + "|log=" + $miPost.Log + "|logBytes=" + $miPost.LogBytes + "|logMtime=" + $miPost.LogMtime + "|logSha12=" + $miPost.LogSha12 + "|note=item count comes from the app's own MENU-INVENTORY line; image identity must carry size + distinctColors + targetForeground as well as sha12")
}
else {
    Say ("MENU-ITEMS|phase=post-capture|absent|reason=" + $miPost.Reason + "|log=" + $miPost.Log + "|note=no inventory line => the item count is UNKNOWN for this image (do not infer it from the height)")
}
$miSummary = 'total=' + $miTotal + '/on=' + $miOn
if (-not $miPost.Ok) { $miSummary = 'absent(' + $miPost.Reason + ')' }

# POST-CAPTURE IDENTITY (evidence, not a new gate): re-hash the dll that the image was attributed to, so
# "the dll did not change under us while the run was in flight" is a reading and not an assumption.
$dllPostSha = '<none>'; $dllPostMtime = '<none>'
if (Test-Path -LiteralPath $dllPath) {
    $dfp = Get-Item -LiteralPath $dllPath
    $dllPostSha = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.Substring(0, 12)
    $dllPostMtime = $dfp.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
}
Say ("POST-CAPTURE-IDENTITY|dllSha256_12=" + $dllPostSha + "|dllMtime=" + $dllPostMtime + "|identicalToPre=" + (($dllPostSha -eq $dllSha) -and ($dllPostMtime -eq $dllMtime)) + "|capturedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))

# keep the provenance next to the evidence (one artifact, not two): append it to the sidecar the capturer wrote
if ((Test-Path -LiteralPath $Sidecar) -and (Test-Path -LiteralPath $Out)) {
    $append = @(
        ''
        '--- PROVENANCE (appended by capture-shell-flyout.ps1) ---'
        ('provenance          = ' + $provLine)
        ('independent verify  = ' + $verdict + ' | ' + $detail)
        ('image size          = ' + $capW + 'x' + $capH + '  distinctColors=' + $capColors + '  (identity is NOT sha12 alone: size/colors/foreground are part of it -- captain 2026-09-12)')
        ('targetForeground    = ' + $targetFg + '  (a NON-BLANK claim requires True)')
        ('menu items          = ' + $miSummary + '  (from the app MENU-INVENTORY line)')
        ('png                 = ' + $Out + ' (' + $pngBytes + ' B, sha256_12=' + $pngSha + ')')
        ('NOTE: this image came from a WORKING-TREE build; the dirty list above is part of its identity.')
    )
    [System.IO.File]::AppendAllText($Sidecar, (($append -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    Say ('SIDECAR-APPENDED|' + $Sidecar + '|bytes=' + (Get-Item -LiteralPath $Sidecar).Length)
}

$leakFail = $false
if (-not $KeepRunning) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    # MEASURED 03:49:48 (control 1 of ruling B): counting instances IMMEDIATELY after Stop-Process raced with
    # termination -- mineLeft=1 printed a FAIL although the process was already dying and gone a moment later.
    # Wait for OUR pid to actually exit (bounded), then count.
    $waitMs = 0
    while (-not $p.HasExited -and $waitMs -lt 5000) { Start-Sleep -Milliseconds 250; $waitMs += 250 }
    Say ("STOPPED|pid=" + $p.Id + "|exitWaitMs=" + $waitMs + "|hasExited=" + $p.HasExited)
    # captain 2026-09-12: a capture run must PROVE it left no instance behind (the next agent's pre-check
    # must not find a stray window of ours).
    # BUGFIX (measured 03:28:43): the old check counted EVERY AIPlayer.Shell process and printed
    # `clean=False` + a FAIL even though OUR pid had been stopped -- the extra instance was someone else's,
    # started while the capture ran. "Someone else's window" is not "we leaked a window", and reporting it as
    # FAIL would train the reader to ignore the line. Attribution is now printed separately.
    # t179 BUGFIX (measured 2026-09-12 17:5x, evidence: shell/Tests/evidence/capture-flyout-sandbox.txt):
    # the check below used to be a SINGLE sample of an integer pid comparison plus a path string compare, and
    # it printed FAIL for an instance that an operator re-scan 1-2 s later could not find at all (both t151
    # arms ended EXIT=1 on that line). It now (a) keys identity on pid + exe path + OUR start time -- the same
    # triple the APPPID discovery above uses -- and (b) re-scans inside a bounded window, so a candidate that
    # is gone within it is reported as transient instead of failing the run. It is NOT relaxed: a process that
    # is ours by all three fields and is still there after the window still FAILs (positive control =
    # -SelfTestLeak case A).
    $scan = Get-LeakScan 'AIPlayer.Shell' ([int]$p.Id) $fi.FullName $ourStartTicks 2000 250
    $mineLeft = [int]$scan.MineLeft
    $othersN = [int]$scan.Others
    $othersDetail = [string]$scan.OthersDetail
    $instTotal = $mineLeft + $othersN
    Say ("POST-STOP-IDENTITY|ourPid=" + $p.Id + "|ourPath=" + $fi.FullName + "|ourStart=" + (Format-Ticks $ourStartTicks) + "|samples=" + $scan.SamplesN + "|windowMs=" + $scan.ElapsedMs + "|rule=pid + exe path + start time, re-scanned inside a bounded window")
    if ($mineLeft -gt 0 -or $scan.FirstMine -gt 0) {
        Say ("POST-STOP-OUR-PID|pid=" + $p.Id + "|path=" + $scan.MinePath + "|start=" + (Format-Ticks $scan.MineTicks) + "|isOurs=" + $scan.IsOurs + "|firstSampleMine=" + $scan.FirstMine + "|note=pid equality alone cannot separate 'our window leaked' from 'pid was reused by another process'; the start time can, and the re-scan says whether it is still there")
    }
    # captain ruling (B) condition 4: with foreign instances tolerated, a bare "instances=0" would be a FALSE
    # statement -- so the tolerated path prints mine/others/detail and NEVER claims an empty field.
    if ($AllowForeignInstances) {
        Say ("POST-STOP|mine=" + $mineLeft + "|others=" + $othersN + "|othersDetail=[" + $othersDetail + "]|clean=" + ($mineLeft -eq 0) + "|note=this run only ever stops the pid it started; foreign instances are reported, not claimed absent")
    }
    else {
        Say ("POST-STOP|instances=" + $instTotal + "|mine=" + $mineLeft + "|others=" + $othersN + "|othersDetail=[" + $othersDetail + "]|clean=" + ($mineLeft -eq 0))
    }
    if ($othersN -gt 0) {
        Say ("FOREIGN-INSTANCES-TOLERATED|count=" + $othersN + "|others=[" + $othersDetail + "]|note=these are NOT ours (we only ever stop the pid we started); they do not invalidate the capture because window attribution uses target pid + exe path + start time")
    }
    if ($scan.Resolved) {
        Say ("POST-STOP-RESOLVED|mine=" + $scan.FirstMine + "->0|windowMs=" + $scan.ElapsedMs + "|note=the candidate seen at the first sample was GONE inside the re-scan window (the t151 false-positive shape); a transient listing is not a leak of ours and does NOT fail the run")
    }
    if ($scan.Persisted -and $scan.IsOurs) { $leakFail = $true; Say 'FAIL|OUR OWN shell instance survived the stop; kill it by hand before anyone else runs' }
    if ($mineLeft -gt 0 -and -not $scan.IsOurs) { Say 'POST-STOP-NOTE|a process carries our pid but its exe path or start time is NOT ours => treated as pid reuse, not as a leak of ours (identity = pid + path + start time)' }
}
# ---------------------------------------------------------------------------------------------
# SUMMARY (LAST line -- the anchor; captain 2026-09-12). `fail` and `inconclusive` are SEPARATE columns: an
# unmet capture precondition is never counted as a failure, and only `pass` may be read as "we have an image".
# The process exit code stays 0/1 for callers, but the VERDICT is this line (README 11.9: the exit code is not
# the verdict). Accounting is self-checked so a counter can never drift away from the reading it counts.
# ---------------------------------------------------------------------------------------------
$nPass = 0; $nInconclusive = 0; $nFail = 0
if ($outcome -eq 'pass') { $nPass = 1 } elseif ($outcome -eq 'inconclusive') { $nInconclusive = 1 } else { $nFail = 1 }
Say ("SUMMARY|image=" + $outcome + "|reason=" + $outcomeReason + "|independentVerdict=" + $verdict + "|size=" + $capW + "x" + $capH + "|distinctColors=" + $capColors + "|targetForeground=" + $targetFg + "|menu=" + $miSummary + "|attempts=" + $attUsed + "|attNonBlank=" + $attNonBlank + "|attBlank=" + $attBlank + "|attNoPng=" + $attNoPng + "|pass=" + $nPass + "|inconclusive=" + $nInconclusive + "|fail=" + $nFail + "|png=" + $pngSha + "|bytes=" + $pngBytes + "|capExit=" + $capExit + "|leakFail=" + $leakFail + "|capturedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
$outcomeSum = $nPass + $nInconclusive + $nFail
$attSum = $attNonBlank + $attBlank + $attNoPng
Say ("SELF-CHECK|runs=1|pass=" + $nPass + "|inconclusive=" + $nInconclusive + "|fail=" + $nFail + "|sum=" + $outcomeSum + "|equal=" + ($outcomeSum -eq 1) + "|attemptSum=" + $attSum + "|attempts=" + $attUsed + "|attemptsEqual=" + ($attSum -eq $attUsed))
if ($leakFail) { exit 1 }
if ($outcome -eq 'pass' -and $capExit -eq 0) { exit 0 }
exit 1
