# popup-capture.ps1 -- capture ONE popup window (light-dismiss MenuFlyout / ContentDialog / ContextMenuStrip)
# with PrintWindow(..., PW_RENDERFULLCONTENT) and a hard privacy guard.
#
# WHY THIS EXISTS (real incident, not hypothetical): a MenuFlyout is a light-dismiss popup in its own HWND.
# Forcing it to the foreground CLOSES it; not forcing it means a screen copy grabs the whole user desktop.
# A capture of the user's private window was produced once by the old tool path. This tool makes that
# structurally impossible: it has NO screen-copy code path at all, it refuses desktop/shell/full-screen
# targets, and it refuses any window that does not belong to the PID you name.
#
# HARD RULES (each one is a guard that must be able to fail):
#   G1 no CopyFromScreen anywhere -- the only channel is PrintWindow on an explicit HWND
#   G2 target HWND must belong to the SAME process as -Pid (GetWindowThreadProcessId) and that process
#      must not be a shell/system process (explorer / dwm / winlogon / csrss / System / Idle)
#   G3 desktop / shell window classes are refused: Progman / WorkerW / Shell_TrayWnd / Shell_SecondaryTrayWnd
#      / Button / SysListView32 / ConsoleWindowClass (the PowerShell console is not evidence)
#   G4 full-screen targets are refused: if the window rect covers >= 95% of the virtual desktop, refuse
#      (this tool deliberately has NO full-screen capture capability)
#   G5 no focus stealing OF THE TARGET: SetForegroundWindow / SetActiveWindow / BringWindowToTop / ShowWindow /
#      SetWindowPos / AttachThreadInput are NOT called anywhere in this file (grep-able, comments excluded), and at
#      runtime the foreground HWND before/after is recorded. The hard gate fires ONLY when the foreground moved
#      to a window THAT BELONGS TO THE TARGET PID -- the only observable that could mean "we stole it". An
#      unrelated foreground change (e.g. the human user working on this shared machine) is RECORDED, not blocked.
#      (Revised after a real false refusal: the old "any change => refuse" rule refused a good capture of our own
#      settings page while the user was using the same machine.)
#   G6 a blank result is not written: the PNG is only written after a non-blank check (mean/variance);
#      all-white / all-black => exit 1 and NO file
#   G7 nothing is written unless the caller names an output path; with no arguments the tool only prints usage
#
# Exit codes: 0 = captured (png + sidecar .txt written), 1 = refused / failed (no file written), 2 = usage error.
# Sidecar evidence is always .txt (never .log) and carries the source assertion: hwnd + pid + process + class
# + title + rect + channel + foreground before/after + png hash/size + non-blank metrics.
#
# ASCII-only on purpose (PS 5.1 decodes BOM-less UTF-8 scripts as ANSI).

[CmdletBinding()]
param(
    # NOTE: the parameter must NOT be named $Pid -- that name is PowerShell's read-only automatic variable
    # (current process id), and binding it throws "Cannot overwrite variable Pid because it is read-only".
    [Alias('Pid')][int]$TargetPid = 0,
    [long]$Hwnd = 0,
    [switch]$Auto,
    [switch]$List,
    [string]$ExpectClassLike = '',
    [string]$Out = '',
    [string]$Sidecar = ''
)

$ErrorActionPreference = 'Stop'

function Write-Err([string]$msg) { Write-Output ("ERROR|" + $msg) }
function Usage-Exit([string]$msg) {
    Write-Err $msg
    Write-Output "USAGE|popup-capture.ps1 -Pid <target pid> (-Hwnd <hwnd> | -Auto) -Out <file.png> [-ExpectClassLike <glob>] [-Sidecar <file.txt>]"
    Write-Output "USAGE|popup-capture.ps1 -Pid <target pid> -List"
    Write-Output "GUARD|no screen copy, no full-screen capture, no focus stealing; desktop/shell/foreign windows are refused"
    exit 2
}

$sig = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class PopCap {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
}
'@
[void](Add-Type -TypeDefinition $sig -Language CSharp)
Add-Type -AssemblyName System.Drawing

$PW_RENDERFULLCONTENT = 0x2
$GWL_STYLE = -16
$GWL_EXSTYLE = -20
$WS_POPUP = 0x80000000
$WS_EX_TOOLWINDOW = 0x80
$WS_EX_TOPMOST = 0x8
$DESKTOP_CLASSES = @('Progman', 'WorkerW', 'Shell_TrayWnd', 'Shell_SecondaryTrayWnd', 'Button', 'SysListView32', 'ConsoleWindowClass')
$SHELL_PROCS = @('explorer', 'dwm', 'winlogon', 'csrss', 'wininit', 'services', 'System', 'Idle', 'smss', 'lsass')

function Get-Class([IntPtr]$h) { $sb = New-Object System.Text.StringBuilder 256; [void][PopCap]::GetClassNameW($h, $sb, 256); return $sb.ToString() }
function Get-Title([IntPtr]$h) { $sb = New-Object System.Text.StringBuilder 512; [void][PopCap]::GetWindowTextW($h, $sb, 512); return $sb.ToString() }
function Get-Rect($r) { return ("{0},{1},{2},{3}" -f $r.Left, $r.Top, $r.Right, $r.Bottom) }
function Get-RectW($r) { return [int]($r.Right - $r.Left) }
function Get-RectH($r) { return [int]($r.Bottom - $r.Top) }

if ($TargetPid -le 0 -and -not $List) { Usage-Exit "missing -Pid" }
if ($TargetPid -le 0 -and $List) { Usage-Exit "missing -Pid for -List" }

# ---- target process must exist and must not be a shell/system process (G2) ----
$proc = $null
try { $proc = Get-Process -Id $TargetPid -ErrorAction Stop } catch { Write-Err ("target pid not found: " + $TargetPid); exit 1 }
$pname = $proc.ProcessName
if ($SHELL_PROCS -contains $pname) {
    Write-Err ("refused: target process is a shell/system process (" + $pname + " pid=" + $TargetPid + ") -- G2")
    exit 1
}

# ---- enumerate windows of that pid (top-level + children) ----
$cands = New-Object System.Collections.ArrayList
$cb = [PopCap+EnumWindowsProc]{
    param($h, $l)
    $wpid = 0
    [void][PopCap]::GetWindowThreadProcessId($h, [ref]$wpid)
    if ($wpid -eq $TargetPid) {
        $r = New-Object PopCap+RECT
        [void][PopCap]::GetWindowRect($h, [ref]$r)
        $st = [PopCap]::GetWindowLong($h, $GWL_STYLE)
        $ex = [PopCap]::GetWindowLong($h, $GWL_EXSTYLE)
        $cls = Get-Class $h
        [void]$cands.Add([pscustomobject]@{
            Hwnd = [long]$h; Class = $cls; Title = (Get-Title $h); Visible = [PopCap]::IsWindowVisible($h)
            W = (Get-RectW $r); H = (Get-RectH $r); Rect = (Get-Rect $r)
            Style = ("0x{0:X8}" -f $st); ExStyle = ("0x{0:X8}" -f $ex)
            IsPopup = (($st -band $WS_POPUP) -ne 0); IsTool = (($ex -band $WS_EX_TOOLWINDOW) -ne 0)
        })
        # popups may be owned/child windows: enumerate one level of children as well
        $cb2 = [PopCap+EnumWindowsProc]{
            param($ch, $l2)
            $cpid = 0
            [void][PopCap]::GetWindowThreadProcessId($ch, [ref]$cpid)
            if ($cpid -eq $TargetPid) {
                $r2 = New-Object PopCap+RECT
                [void][PopCap]::GetWindowRect($ch, [ref]$r2)
                $st2 = [PopCap]::GetWindowLong($ch, $GWL_STYLE)
                $ex2 = [PopCap]::GetWindowLong($ch, $GWL_EXSTYLE)
                [void]$cands.Add([pscustomobject]@{
                    Hwnd = [long]$ch; Class = (Get-Class $ch); Title = (Get-Title $ch); Visible = [PopCap]::IsWindowVisible($ch)
                    W = (Get-RectW $r2); H = (Get-RectH $r2); Rect = (Get-Rect $r2)
                    Style = ("0x{0:X8}" -f $st2); ExStyle = ("0x{0:X8}" -f $ex2)
                    IsPopup = (($st2 -band $WS_POPUP) -ne 0); IsTool = (($ex2 -band $WS_EX_TOOLWINDOW) -ne 0)
                })
            }
            return $true
        }
        [void][PopCap]::EnumChildWindows($h, $cb2, [IntPtr]::Zero)
    }
    return $true
}
[void][PopCap]::EnumWindows($cb, [IntPtr]::Zero)

# de-duplicate by hwnd
$seen = @{}
$uniq = New-Object System.Collections.ArrayList
foreach ($c in $cands) { if (-not $seen.ContainsKey($c.Hwnd)) { $seen[$c.Hwnd] = 1; [void]$uniq.Add($c) } }

if ($List) {
    Write-Output ("LIST|pid=" + $TargetPid + " process=" + $pname + " windows=" + $uniq.Count + " sampled=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
    foreach ($c in $uniq) {
        Write-Output ("CAND|hwnd=" + $c.Hwnd + "|class=" + $c.Class + "|title=" + $c.Title + "|visible=" + $c.Visible + "|rect=" + $c.Rect + "|size=" + $c.W + "x" + $c.H + "|style=" + $c.Style + "|exstyle=" + $c.ExStyle + "|popup=" + $c.IsPopup + "|tool=" + $c.IsTool)
    }
    # LIST-SHAPE (captain 2026-09-12): "popup image identity is not sha12 alone" needs the SHAPE as a reading.
    # The measured pair was 164x466 (a menu carrying 11 items) vs 164x378, so the shape is printed here as a
    # distribution instead of leaving the next reader to eyeball the CAND lines. Item count itself comes from the
    # app's own MENU-INVENTORY line (see capture-shell-flyout.ps1 MENU-ITEMS), never from this height.
    $shape = (@($uniq) | ForEach-Object { [string]$_.W + 'x' + [string]$_.H }) -join ';'
    $popupN = @($uniq | Where-Object { $_.IsPopup }).Count
    $toolN = @($uniq | Where-Object { $_.IsTool }).Count
    $visN = @($uniq | Where-Object { $_.Visible }).Count
    Write-Output ("LIST-SHAPE|windows=" + $uniq.Count + "|visible=" + $visN + "|popups=" + $popupN + "|tools=" + $toolN + "|sizes=[" + $shape + "]|distinctSizes=" + @($uniq | ForEach-Object { [string]$_.W + 'x' + [string]$_.H } | Sort-Object -Unique).Count + "|note=size is part of the evidence identity (with distinctColors and the foreground flag); the item count is a separate reading (MENU-ITEMS), not derived from height")
    exit 0
}

if ($Out -eq '') { Usage-Exit "missing -Out (nothing is written without an explicit path)" }

$screenW = [PopCap]::GetSystemMetrics(78)   # SM_CXVIRTUALSCREEN
$screenH = [PopCap]::GetSystemMetrics(79)   # SM_CYVIRTUALSCREEN
if ($screenW -le 0 -or $screenH -le 0) { $screenW = [PopCap]::GetSystemMetrics(0); $screenH = [PopCap]::GetSystemMetrics(1) }

# ---- choose the target window ----
$target = $null
if ($Hwnd -gt 0) {
    $h = [IntPtr]$Hwnd
    if (-not [PopCap]::IsWindow($h)) { Write-Err ("refused: hwnd is not a window: " + $Hwnd); exit 1 }
    $wpid = 0
    [void][PopCap]::GetWindowThreadProcessId($h, [ref]$wpid)
    if ($wpid -ne $TargetPid) { Write-Err ("refused: hwnd " + $Hwnd + " belongs to pid " + $wpid + ", not to -Pid " + $TargetPid + " -- G2"); exit 1 }
    foreach ($c in $uniq) { if ($c.Hwnd -eq $Hwnd) { $target = $c } }
    if ($target -eq $null) {
        $r0 = New-Object PopCap+RECT
        [void][PopCap]::GetWindowRect($h, [ref]$r0)
        $target = [pscustomobject]@{ Hwnd = $Hwnd; Class = (Get-Class $h); Title = (Get-Title $h); Visible = [PopCap]::IsWindowVisible($h); W = (Get-RectW $r0); H = (Get-RectH $r0); Rect = (Get-Rect $r0); Style = ("0x{0:X8}" -f [PopCap]::GetWindowLong($h, $GWL_STYLE)); ExStyle = ("0x{0:X8}" -f [PopCap]::GetWindowLong($h, $GWL_EXSTYLE)); IsPopup = $false; IsTool = $false }
    }
}
elseif ($Auto) {
    $pool = @($uniq | Where-Object { $_.Visible -and $_.Class -ne '' -and ($DESKTOP_CLASSES -notcontains $_.Class) -and $_.W -gt 20 -and $_.H -gt 20 })
    if ($ExpectClassLike -ne '') { $pool = @($pool | Where-Object { $_.Class -like $ExpectClassLike }) }
    $popups = @($pool | Where-Object { $_.IsPopup -or $_.IsTool })
    if ($popups.Count -gt 0) { $pool = $popups }
    if ($pool.Count -eq 0) { Write-Err ("no popup candidate for pid " + $TargetPid + " (use -List to inspect)"); exit 1 }
    $target = ($pool | Sort-Object -Property @{ Expression = { $_.W * $_.H } } | Select-Object -First 1)
}
else {
    Usage-Exit "either -Hwnd or -Auto is required"
}

# ---- G3 desktop / shell class guard ----
if ($DESKTOP_CLASSES -contains $target.Class) {
    Write-Err ("refused: desktop/shell window class '" + $target.Class + "' -- G3 (no desktop capture)")
    exit 1
}

# ---- G4 full-screen guard ----
$area = [double]$target.W * [double]$target.H
$screenArea = [double]$screenW * [double]$screenH
if ($screenArea -gt 0 -and ($area / $screenArea) -ge 0.95) {
    Write-Err ("refused: target window covers " + [Math]::Round(100 * $area / $screenArea, 1) + "% of the virtual desktop (" + $target.W + "x" + $target.H + ") -- G4 (full-screen capture is intentionally not supported)")
    exit 1
}
if ($target.W -le 0 -or $target.H -le 0) { Write-Err "refused: empty window rect"; exit 1 }

# G5 observation window: sample the foreground at ENTRY (before enumeration/capture) and again right after the
# capture, so that any focus change that happens *while this tool runs* is observable -- that is the only
# window in which a regression (the tool itself stealing focus of the target) could act.
$fgBefore = [PopCap]::GetForegroundWindow()

# ---- capture: PrintWindow only (G1: no screen copy anywhere) ----
# Primary flag = PW_RENDERFULLCONTENT (0x2) per the task. Some popups render blank under 0x2 while 0x0 works
# (and vice versa), so we try both and keep the first NON-BLANK frame; the sidecar records which flag won.
function Get-FrameMetrics($bmp, [int]$w, [int]$h) {
    $sum = 0.0; $sumSq = 0.0; $n = 0; $nonBlack = 0; $white = 0
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $lum = (0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B)
            $sum += $lum; $sumSq += ($lum * $lum); $n++
            if ($c.R -gt 12 -or $c.G -gt 12 -or $c.B -gt 12) { $nonBlack++ }
            if ($c.R -ge 250 -and $c.G -ge 250 -and $c.B -ge 250) { $white++ }
        }
    }
    $m = [Math]::Round($sum / $n, 3)
    $v = [Math]::Round(($sumSq / $n) - ($m * $m), 3)
    if ($v -lt 0) { $v = 0.0 }
    return @{ Mean = $m; Var = $v; Std = [Math]::Round([Math]::Sqrt($v), 3); NonBlackPct = [Math]::Round(100.0 * $nonBlack / $n, 3); White = $white; Pixels = $n }
}

$triedFlags = @()
$winner = $null
$usedFlag = 0
foreach ($flag in @([uint32]0x2, [uint32]0x0)) {
    $bmp = New-Object System.Drawing.Bitmap($target.W, $target.H)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    $okLocal = $false
    try { $okLocal = [PopCap]::PrintWindow([IntPtr]$target.Hwnd, $hdc, $flag) } finally { $gfx.ReleaseHdc($hdc); $gfx.Dispose() }
    $mtr = Get-FrameMetrics $bmp $target.W $target.H
    $triedFlags += ("0x{0:X}(ok={1},nonBlack={2}%,std={3})" -f $flag, $okLocal, $mtr.NonBlackPct, $mtr.Std)
    $blank = ($mtr.NonBlackPct -le 0.001) -or (($mtr.NonBlackPct -ge 99.9) -and ($mtr.Std -lt 1.0))
    if (-not $blank) { $winner = $bmp; $usedFlag = $flag; $ok = $okLocal; $metrics = $mtr; break }
    $bmp.Dispose()
}
$fgAfter = [PopCap]::GetForegroundWindow()

# G5 (revised after a real false-refusal): a focus change is only OUR fault if the foreground moved TO A WINDOW
# THAT BELONGS TO THE TARGET PID. This machine is shared with the human user, who may change the foreground at
# any moment -- the old rule ("any change => refuse") refused a perfectly good capture of our own settings page.
# The static guarantee (no SetForegroundWindow/SetWindowPos/... anywhere in this file) is the primary control;
# this runtime check exists to catch REGRESSIONS, i.e. someone making the tool steal focus of the target process.
$fgChanged = ($fgBefore -ne $fgAfter)
$fgAfterPid = 0
if ($fgAfter -ne [IntPtr]::Zero) { [void][PopCap]::GetWindowThreadProcessId($fgAfter, [ref]$fgAfterPid) }
$focusStolen = $false
if ($fgChanged -and $fgAfterPid -eq $TargetPid) { $focusStolen = $true }

if ($focusStolen) {
    Write-Err ("refused: foreground moved to a window of the TARGET process (" + $fgBefore + " -> " + $fgAfter + ", fgAfterPid=" + $fgAfterPid + " == target pid) -- G5 (no focus stealing of the target allowed)")
    if ($winner -ne $null) { $winner.Dispose() }
    exit 1
}
if ($winner -eq $null) {
    Write-Err ("blank frame under every PrintWindow flag (" + ($triedFlags -join ' ') + ") -- G6 (nothing written)")
    exit 1
}

$dir = Split-Path -Parent $Out
if ($dir -ne '' -and -not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Force -Path $dir) }
$winner.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$winner.Dispose()

$mean = $metrics.Mean
$std = $metrics.Std
$nonBlackPct = $metrics.NonBlackPct
$white = $metrics.White

$fi = Get-Item -LiteralPath $Out
$sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Out).Hash.Substring(0, 12)
$stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')

if ($Sidecar -eq '') { $Sidecar = ($Out -replace '\.png$', '') + '.txt' }
$lines = @()
$lines += "POPUP-CAPTURE SOURCE ASSERTION (sidecar; .txt on purpose, never .log)"
$lines += "sampled            = " + $stamp
$lines += "tool               = shell/tools/popup-capture.ps1 (channel: PrintWindow only)"
$lines += "channel            = printwindow (NO screen copy anywhere in the tool: G1)"
$lines += "printwindow flags  = 0x" + ("{0:X}" -f $usedFlag) + " won (tried: " + ($triedFlags -join ' | ') + ")"
$lines += "hwnd               = " + $target.Hwnd
$lines += "pid                = " + $TargetPid
$lines += "process            = " + $pname
$lines += "window class       = " + $target.Class
$lines += "window title       = " + $target.Title
$lines += "window rect        = " + $target.Rect + " (" + $target.W + "x" + $target.H + ")"
$lines += "window style       = " + $target.Style + " exstyle=" + $target.ExStyle + " popup=" + $target.IsPopup + " tool=" + $target.IsTool
$lines += "virtual desktop    = " + $screenW + "x" + $screenH + " (target covers " + [Math]::Round(100 * $area / $screenArea, 2) + "%)"
$lines += "foreground before  = " + $fgBefore
$lines += "foreground after   = " + $fgAfter + " (fgAfterPid=" + $fgAfterPid + ", changed=" + $fgChanged + ", belongsToTargetPid=" + ($fgAfterPid -eq $TargetPid) + ")"
$lines += "focus gate         = G5 revised: refuse only when the foreground moved to a window of the TARGET pid; an unrelated foreground change is recorded (fgChanged=" + $fgChanged + ", stolen=" + $focusStolen + ")"
$lines += "printwindow ok     = " + $ok
$lines += "png                = " + $Out
$lines += "png bytes          = " + $fi.Length
$lines += "png sha256_12      = " + $sha
$lines += "png size           = " + $target.W + "x" + $target.H
$lines += "luminance mean     = " + $mean
$lines += "luminance stddev   = " + $std
$lines += "nonBlack pct       = " + $nonBlackPct
$lines += "white pixels       = " + $white
[System.IO.File]::WriteAllLines($Sidecar, $lines, (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("CAPTURE|channel=printwindow|flags=0x" + ("{0:X}" -f $usedFlag) + "|hwnd=" + $target.Hwnd + "|pid=" + $TargetPid + "|process=" + $pname + "|class=" + $target.Class + "|title=" + $target.Title + "|rect=" + $target.Rect + "|size=" + $target.W + "x" + $target.H)
Write-Output ("PNG|path=" + $Out + "|bytes=" + $fi.Length + "|sha256_12=" + $sha + "|mean=" + $mean + "|std=" + $std + "|nonBlackPct=" + $nonBlackPct)
Write-Output ("SIDECAR|path=" + $Sidecar + "|bytes=" + (Get-Item -LiteralPath $Sidecar).Length)
Write-Output ("GUARD|focusGate=refuse-only-if-foreground-moved-to-target-pid|focusChanged=" + $fgChanged + "|focusStolenFromTarget=" + $focusStolen + "|fgAfterPid=" + $fgAfterPid + "|printwindowOk=" + $ok + "|noScreenCopy=true|noFullScreenCapture=true")
exit 0
