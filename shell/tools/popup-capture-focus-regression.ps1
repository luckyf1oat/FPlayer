# popup-capture-focus-regression.ps1 -- proves the G5 focus gate of popup-capture.ps1 in BOTH directions (t47).
#
# Case A (must REFUSE, exit != 0, no new file):
#   a target worker process shows a LARGE normal window; the harness holds the foreground itself, starts the
#   capture tool asynchronously and then -- while the tool is running and inside its foreground observation window
#   -- forces the TARGET window to the foreground. That is the regression "the tool steals focus of the target".
#   The target window is deliberately large: the tool's observation window is entry-sample -> capture -> exit-sample
#   and the capture includes a full-pixel scan, so a large surface widens that window and makes the injection
#   deterministic instead of a race.
# Case B (must SUCCEED, exit == 0, file written):
#   the foreground is held by an UNRELATED window (this harness owns it; different pid) for the whole run --
#   reproducing "the human user is working on the same machine", which is exactly what the old
#   "any foreground change => refuse" rule wrongly rejected (it refused a capture of our own settings page).
#
# The harness may use focus-stealing APIs (AttachThreadInput etc.) -- it is a test fixture. The TOOL must not:
# popup-capture.ps1 has no such import (grep-able), which is its primary control.
#
# Invoke with an execution-policy bypass on machines that block .ps1 (this host blocks it):
#    powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture-focus-regression.ps1 -WorkDir %TEMP%\t47-focus
#
# TRAP found while building this: Start-Process -ArgumentList does NOT quote path arguments -- a path containing
# a space ("E:\AI Player\...") is split and powershell.exe reports "does not have a '.ps1' extension".
# Paths handed to Start-Process must be quoted by the caller. (The tool itself is launched through
# System.Diagnostics.Process below, which takes an argument list without that pitfall.)
#
# ASCII-only on purpose.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WorkDir,
    [int]$HoldMs = 25000,
    [int]$InjectDelayMs = 1500
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$sig = @'
using System; using System.Runtime.InteropServices;
public class F47 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowThreadProcessId(IntPtr h, IntPtr x);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
}
'@
[void](Add-Type -TypeDefinition $sig -Language CSharp)

if (-not (Test-Path -LiteralPath $WorkDir)) { [void](New-Item -ItemType Directory -Force -Path $WorkDir) }
$tool = Join-Path $PSScriptRoot 'popup-capture.ps1'
$worker = Join-Path $PSScriptRoot 'popup-capture-target-worker.ps1'
foreach ($f in @($tool, $worker)) { if (-not (Test-Path -LiteralPath $f)) { Write-Output ("ERROR|missing " + $f); exit 2 } }

function Quote-Arg([string]$a) {
    # .NET Framework's ProcessStartInfo has NO ArgumentList (that is .NET Core 2.1+): build the string ourselves
    # and quote anything with a space -- otherwise "E:\AI Player\..." is split (the trap documented at the top).
    if ($a -match '[\s"]') { return '"' + ($a -replace '"', '\"') + '"' }
    return $a
}
function Get-FgPid() {    $h = [F47]::GetForegroundWindow()
    if ($h -eq [IntPtr]::Zero) { return 0 }
    $p = 0
    [void][F47]::GetWindowThreadProcessId($h, [ref]$p)
    return [int]$p
}
function Force-Foreground([IntPtr]$h) {
    $fg = [F47]::GetForegroundWindow()
    $fgThread = 0
    if ($fg -ne [IntPtr]::Zero) { $fgThread = [int][F47]::GetWindowThreadProcessId($fg, [IntPtr]::Zero) }
    $myThread = [int][F47]::GetCurrentThreadId()
    $attached = $false
    if ($fgThread -ne 0 -and $fgThread -ne $myThread) { $attached = [F47]::AttachThreadInput([uint32]$myThread, [uint32]$fgThread, $true) }
    try {
        [void][F47]::BringWindowToTop($h)
        [void][F47]::SetForegroundWindow($h)
    }
    finally {
        if ($attached) { [void][F47]::AttachThreadInput([uint32]$myThread, [uint32]$fgThread, $false) }
    }
}
function Start-Target([string]$hwndFile, [string]$mode, [int]$holdMs, [int]$w, [int]$h) {
    if (Test-Path -LiteralPath $hwndFile) { Remove-Item -LiteralPath $hwndFile -Force }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell.exe'
    $psi.UseShellExecute = $false
    $psi.Arguments = ((@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $worker, '-HwndFile', $hwndFile, '-HoldMs', [string]$holdMs, '-Mode', $mode, '-FormWidth', [string]$w, '-FormHeight', [string]$h) | ForEach-Object { Quote-Arg $_ }) -join ' ')
    $p = [System.Diagnostics.Process]::Start($psi)
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $hwndFile) {
            $txt = ([System.IO.File]::ReadAllText($hwndFile)).Trim()
            if ($txt -ne '' -and $txt -ne '0') { Start-Sleep -Milliseconds 400; return @{ Proc = $p; Hwnd = [long]$txt } }
        }
        Start-Sleep -Milliseconds 200
    }
    return @{ Proc = $p; Hwnd = 0 }
}
function Stop-Target($t) {
    try { if ($t -ne $null -and $t.Proc -ne $null -and -not $t.Proc.HasExited) { $t.Proc.Kill() } } catch { }
}
function FileList([string]$dir) { return (@(Get-ChildItem $dir -File -ErrorAction SilentlyContinue) | Sort-Object Name | ForEach-Object { $_.Name }) -join ',' }
function Start-Tool([int]$targetPid, [long]$hwnd, [string]$out, [string]$tag) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell.exe'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = ((@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $tool, '-TargetPid', [string]$targetPid, '-Hwnd', [string]$hwnd, '-Out', $out) | ForEach-Object { Quote-Arg $_ }) -join ' ')
    $p = [System.Diagnostics.Process]::Start($psi)
    return @{ Proc = $p; Tag = $tag }
}
function Wait-Tool($t) {
    $out = $t.Proc.StandardOutput.ReadToEnd()
    $err = $t.Proc.StandardError.ReadToEnd()
    $t.Proc.WaitForExit()
    $lines = @()
    if ($out -ne '') { $lines += ($out -split "`r?`n") }
    if ($err -ne '') { $lines += ($err -split "`r?`n") }
    return @{ Code = $t.Proc.ExitCode; Lines = ($lines | Where-Object { $_ -ne '' }) }
}

$verdicts = New-Object System.Collections.ArrayList

# shared "unrelated" window owned by this harness (a different pid from any target)
$mine = New-Object System.Windows.Forms.Form
$mine.Text = 't47 unrelated foreground (harness)'
$mine.StartPosition = 'Manual'
$mine.SetBounds(760, 140, 420, 160)
$mine.Show()
Force-Foreground $mine.Handle
Start-Sleep -Milliseconds 400

# ---------------- Case A: focus injected INTO the target process while the tool runs => must REFUSE ----------------
Write-Output "CASE-A|scene=foreground forced onto the TARGET window during the tool run (simulated regression)"
$ha = Join-Path $WorkDir 'a-hwnd.txt'
$ta = Start-Target $ha 'form' $HoldMs 900 600
if ($ta.Hwnd -le 0) { Write-Output "CASE-A|SCENE-NOT-ESTABLISHED worker hwnd not found"; [void]$verdicts.Add('INCONCLUSIVE') }
else {
    Force-Foreground $mine.Handle
    Start-Sleep -Milliseconds 400
    $fgEntryA = Get-FgPid
    $outA = Join-Path $WorkDir 'case-a-should-not-exist.png'
    $before = FileList $WorkDir
    $t = Start-Tool $ta.Proc.Id $ta.Hwnd $outA 'casea'
    # The tool compiles its P/Invoke helper with Add-Type at start (~2-3 s), then samples the foreground at entry,
    # then captures (a 900x600 surface costs ~3 s in the full-pixel scan) and samples again. Injecting twice in
    # that window keeps the test deterministic without relying on a single lucky timestamp.
    Start-Sleep -Milliseconds $InjectDelayMs
    Force-Foreground ([IntPtr]$ta.Hwnd)
    $fgInjected = Get-FgPid
    Start-Sleep -Milliseconds 800
    if (-not $t.Proc.HasExited) { Force-Foreground ([IntPtr]$ta.Hwnd) }
    $r = Wait-Tool $t
    $codeA = $r.Code
    $after = FileList $WorkDir
    $newA = @(Compare-Object ($before -split ',') ($after -split ',') | Where-Object { $_.SideIndicator -eq '=>' }).Count
    $sceneOkA = (($fgEntryA -ne $ta.Proc.Id) -and ($fgInjected -eq $ta.Proc.Id))
    Write-Output ("CASE-A|worker pid=" + $ta.Proc.Id + " target hwnd=" + $ta.Hwnd + "|fgPidAtToolStart=" + $fgEntryA + "|fgPidAfterInjection=" + $fgInjected + "|sceneRequires=start!=target AND injected==target")
    foreach ($line in $r.Lines) { Write-Output ("CASE-A|out| " + [string]$line) }
    Write-Output ("CASE-A|exit=" + $codeA + " newFiles=" + $newA)
    if (-not $sceneOkA) { Write-Output "CASE-A|SCENE-NOT-ESTABLISHED"; [void]$verdicts.Add('INCONCLUSIVE') }
    else {
        $okA = ($codeA -ne 0) -and ($newA -eq 0)
        [void]$verdicts.Add($(if ($okA) { 'PASS' } else { 'FAIL' }))
        Write-Output ("CASE-A|verdict=" + $(if ($okA) { 'PASS' } else { 'FAIL' }))
    }
    Stop-Target $ta
}

# ---------------- Case B: unrelated foreground change => must SUCCEED ----------------
Write-Output "CASE-B|scene=unrelated foreground (human user working) -- must NOT be refused"
$hb = Join-Path $WorkDir 'b-hwnd.txt'
$tb = Start-Target $hb 'form' $HoldMs 360 140
if ($tb.Hwnd -le 0) { Write-Output "CASE-B|SCENE-NOT-ESTABLISHED worker hwnd not found"; [void]$verdicts.Add('INCONCLUSIVE') }
else {
    Force-Foreground $mine.Handle
    Start-Sleep -Milliseconds 400
    $fgEntryB = Get-FgPid
    $outB = Join-Path $WorkDir 'case-b-expected.png'
    $before = FileList $WorkDir
    $t = Start-Tool $tb.Proc.Id $tb.Hwnd $outB 'caseb'
    Start-Sleep -Milliseconds 300
    Force-Foreground $mine.Handle
    $r = Wait-Tool $t
    $codeB = $r.Code
    $after = FileList $WorkDir
    $newB = @(Compare-Object ($before -split ',') ($after -split ',') | Where-Object { $_.SideIndicator -eq '=>' }).Count
    $sceneOkB = ($fgEntryB -ne $tb.Proc.Id)
    Write-Output ("CASE-B|worker pid=" + $tb.Proc.Id + " target hwnd=" + $tb.Hwnd + " harness pid=" + $PID + "|fgPidAtToolStart=" + $fgEntryB + "|sceneRequires=start!=target")
    foreach ($line in $r.Lines) { Write-Output ("CASE-B|out| " + [string]$line) }
    Write-Output ("CASE-B|exit=" + $codeB + " newFiles=" + $newB + " pngExists=" + (Test-Path -LiteralPath $outB))
    if (-not $sceneOkB) { Write-Output "CASE-B|SCENE-NOT-ESTABLISHED"; [void]$verdicts.Add('INCONCLUSIVE') }
    else {
        $okB = ($codeB -eq 0) -and (Test-Path -LiteralPath $outB)
        [void]$verdicts.Add($(if ($okB) { 'PASS' } else { 'FAIL' }))
        Write-Output ("CASE-B|verdict=" + $(if ($okB) { 'PASS' } else { 'FAIL' }))
    }
    Stop-Target $tb
}

$mine.Close(); $mine.Dispose()
$pass = @($verdicts | Where-Object { $_ -eq 'PASS' }).Count
$fail = @($verdicts | Where-Object { $_ -eq 'FAIL' }).Count
$inc = @($verdicts | Where-Object { $_ -eq 'INCONCLUSIVE' }).Count
Write-Output ("SUMMARY|caseA=" + $verdicts[0] + "|caseB=" + $verdicts[1] + "|pass=" + $pass + "|fail=" + $fail + "|inconclusive=" + $inc)
if ($fail -gt 0) { exit 1 }
if ($inc -gt 0) { exit 3 }
exit 0
