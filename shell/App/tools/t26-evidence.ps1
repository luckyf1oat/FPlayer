# t26 / U-A evidence run: launch the shell, capture it, then read the pixels back with an
# explicit client-area origin (the 2026-09-11 first attempt forgot the caption offset and
# started the sidebar scan at x=200 -> every sample was off by the window frame).
#
# Everything this script claims is printed with its coordinate and its judgement rule, and the
# raw numbers go into <OutDir>\<NamePrefix>-evidence.txt (UTF-8), so the numbers can be
# re-derived by hand from the PNG alone.
#
# Row text/colour evidence is driven by UI Automation rectangles (read-only) instead of guessed
# offsets, because a second agent clicked this window during the 23:00 run and the row layout
# answered with a different selection state.
#
# ASCII-only script text on purpose (PowerShell 5.1 decoding trap).
param(
  [Parameter(Mandatory=$true)][string]$Exe,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [int]$WaitSec = 22,
  [string]$NamePrefix = 'ui-t26',
  [string]$RailFailUrl = 'http://127.0.0.1:9/',
  [string]$StartPage = 'home',
  [switch]$NoStaleKill,   # 历史调用方会传它；已改为空操作（不再有任何"杀别人"的路径）
  [switch]$ForceTakeOver  # 显式允许"在场时起窗"（仍然不杀任何实例，只标注读数可能被污染）
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sig = @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class W4 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@
Add-Type -TypeDefinition $sig -Language CSharp

if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
$logPath = Join-Path $OutDir ($NamePrefix + '-evidence.txt')
$lines = New-Object System.Collections.Generic.List[string]
function Say([string]$s) { Write-Host $s; $lines.Add($s) }
function Flush() { $lines | Out-File -LiteralPath $logPath -Encoding utf8 }

Say ("EVIDENCE RUN t26 start=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
Say ("CONFIG RailFailUrl=" + $RailFailUrl + " StartPage=" + $StartPage)

# Never kill a process this script did not start (team rule: nobody may end another instance).
# The old code killed EVERY AIPlayer.Shell by name, which would kill a user's or teammate's live
# window (it really happened once). Now: detect only, never kill; refuse to start while one is
# present, unless the caller explicitly passes -ForceTakeOver.
$stale = @(Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue)
if ($stale.Count -gt 0) {
  foreach ($p in $stale) { Say ("STALE-PRESENT pid=" + $p.Id + " (not ours, left running)") }
  if (-not $ForceTakeOver) {
    Say "ABORT foreign AIPlayer.Shell present; rerun with -ForceTakeOver if you really mean it"
    Flush
    exit 5
  }
}

$env:SHELL_SELFTEST_RAIL_FAIL = $RailFailUrl
$env:SHELL_START_PAGE = $StartPage
$proc = Start-Process -FilePath $Exe -PassThru
Say ("PID=" + $proc.Id + " exe=" + $Exe)
Start-Sleep -Seconds $WaitSec

if ($proc.HasExited) { Say ("EXITED code=" + $proc.ExitCode); Flush; exit 3 }
Say ("ALIVE pid=" + $proc.Id)

$script:best = [IntPtr]::Zero
$script:bestArea = 0
$script:bestTitle = ''
$script:bestClass = ''
$script:OwnerPid = $proc.Id
$cb = [W4+EnumProc]{
  param($h, $l)
  $owner = 0
  [void][W4]::GetWindowThreadProcessId($h, [ref]$owner)
  if ($owner -ne $script:OwnerPid) { return $true }
  if (-not [W4]::IsWindowVisible($h)) { return $true }
  $r = New-Object W4+RECT
  [void][W4]::GetWindowRect($h, [ref]$r)
  $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
  if ($area -gt $script:bestArea) {
    $script:bestArea = $area
    $script:best = $h
    $sb = New-Object System.Text.StringBuilder 512
    [void][W4]::GetWindowTextW($h, $sb, 512)
    $script:bestTitle = $sb.ToString()
    $sb2 = New-Object System.Text.StringBuilder 512
    [void][W4]::GetClassNameW($h, $sb2, 512)
    $script:bestClass = $sb2.ToString()
  }
  return $true
}
[void][W4]::EnumWindows($cb, [IntPtr]::Zero)
$hwnd = $script:best
if ($hwnd -eq [IntPtr]::Zero) { Say 'NO-VISIBLE-WINDOW'; Flush; exit 4 }

$wr = New-Object W4+RECT
[void][W4]::GetWindowRect($hwnd, [ref]$wr)
$cr = New-Object W4+RECT
[void][W4]::GetClientRect($hwnd, [ref]$cr)
$pt = New-Object W4+POINT
$pt.X = 0; $pt.Y = 0
[void][W4]::ClientToScreen($hwnd, [ref]$pt)
$dpi = [W4]::GetDpiForWindow($hwnd)

$winW = $wr.Right - $wr.Left
$winH = $wr.Bottom - $wr.Top
$dx = $pt.X - $wr.Left
$dy = $pt.Y - $wr.Top
$clientW = $cr.Right
Say ("WINDOW hwnd=$hwnd class='$($script:bestClass)' title='$($script:bestTitle)' winrect=$($wr.Left),$($wr.Top),$($wr.Right),$($wr.Bottom) size=${winW}x${winH}")
Say ("CLIENT rect=0,0,$($cr.Right),$($cr.Bottom) size=$($cr.Right)x$($cr.Bottom) clientScreen=$($pt.X),$($pt.Y) dpi=$dpi")
Say ("ORIGIN dx=$dx dy=$dy (bitmap_of_client(x,y) = (x+$dx, y+$dy)); dpiScale=$([math]::Round($dpi/96.0,4))")

# ===== H1 capture guards (allow-list entry: shell/tools/privacy-allowlist.txt) =====
# Guard 1: never screen-copy a desktop-class window (that is the user's desktop, not our UI).
if ($script:bestClass -match '^(Progman|WorkerW|Shell_TrayWnd)$') {
  Say ("REFUSED desktop-class window class='" + $script:bestClass + "'; nothing written")
  Flush
  exit 6
}
# Guard 2: a rect covering >=95% of the virtual desktop is a de-facto full-screen grab.
$vsw = [W4]::GetSystemMetrics(78); $vsh = [W4]::GetSystemMetrics(79)
if ($vsh -gt 0) {
  $cover = ($winW * $winH) / [double]($vsw * $vsh)
  if ($cover -ge 0.95) {
    Say ("REFUSED rect covers " + [math]::Round($cover * 100, 1) + "% of the virtual desktop (" + $vsw + "x" + $vsh + "); nothing written")
    Flush
    exit 6
  }
}
# Guard 3 (pid ownership) is enforced while picking the HWND: GetWindowThreadProcessId must equal $script:OwnerPid.
# Guard 4: the written provenance, so every PNG can be traced back to one window.
Say ("SOURCE-ASSERTION hwnd=$hwnd pid=$($proc.Id) rect=$($wr.Left),$($wr.Top),$($wr.Right),$($wr.Bottom) size=${winW}x${winH} channel=printwindow+copyfromscreen")

function Capture([string]$suffix) {
  [void][W4]::SetForegroundWindow($hwnd)
  Start-Sleep -Milliseconds 900
  $b = New-Object System.Drawing.Bitmap $winW, $winH
  $g = [System.Drawing.Graphics]::FromImage($b)
  $hdc = $g.GetHdc()
  $ok = [W4]::PrintWindow($hwnd, $hdc, 2)
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $p = Join-Path $OutDir ($NamePrefix + $suffix + '.png')
  $b.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
  Say ("CAPTURE-PRINTWINDOW$suffix ok=$ok -> $p bytes=$((Get-Item -LiteralPath $p).Length) size=${winW}x${winH}")
  return $b
}

$bmp = Capture ''
# screen-copy channel as a cross check (D3D surfaces sometimes come back blank under PrintWindow)
[void][W4]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0043)
[void][W4]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1200
$bmp2 = New-Object System.Drawing.Bitmap $winW, $winH
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.CopyFromScreen($wr.Left, $wr.Top, 0, 0, (New-Object System.Drawing.Size $winW, $winH))
$g2.Dispose()
$scPath = Join-Path $OutDir ($NamePrefix + '-screencapture.png')
$bmp2.Save($scPath, [System.Drawing.Imaging.ImageFormat]::Png)
Say ("CAPTURE-SCREENCOPY -> $scPath bytes=$((Get-Item -LiteralPath $scPath).Length)")

function HexAt($bb, [int]$x, [int]$y) {
  $c = $bb.GetPixel($x, $y)
  return ('#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B)
}

# ============ (2) sidebar width: scan from x = 0, state the rule ============
Say ''
Say '=== CRITERION: sidebar width 256 +/- 2 px ==='
Say 'RULE: scan bitmap row y = clientY + dy, starting at bitmap x = 0 (NOT at some interior x);'
Say '      the first x whose colour equals PageBg #222222 and stays #222222 for >= 8 px is the'
Say '      rail edge; railWidthPx = edgeBitmapX - dx.'
foreach ($cy in 300, 380, 640) {
  $by = $cy + $dy
  if ($by -ge $winH) { continue }
  $edge = -1
  for ($x = 0; $x -lt $winW - 8; $x++) {
    if ((HexAt $bmp $x $by) -ne '#222222') { continue }
    $all = $true
    for ($k = 1; $k -lt 8; $k++) { if ((HexAt $bmp ($x + $k) $by) -ne '#222222') { $all = $false; break } }
    if ($all) { $edge = $x; break }
  }
  $near = ''
  for ($x = [Math]::Max(0, $edge - 3); $x -le $edge + 3; $x++) { $near += ("$x=" + (HexAt $bmp $x $by) + ' ') }
  Say ("RAILSCAN clientY=$cy bitmapY=$by edgeBitmapX=$edge edgeClientX=$($edge - $dx) railWidthPx=$($edge - $dx) aroundEdge=[ $near]")
}

# ============ (3) the five token points from spec section 1 ============
Say ''
Say '=== CRITERION: five token samples (spec section 1) ==='
$script:misses = New-Object System.Collections.ArrayList
function Probe([string]$name, [int]$cx, [int]$cy, [string]$expect, [string]$tag = '') {
  $bx = $cx + $dx
  $by = $cy + $dy
  $got = HexAt $bmp $bx $by
  $hit = ($got -eq $expect)
  $note = ''
  if (-not $hit) {
    $found = $null
    for ($r = 1; $r -le 8 -and -not $found; $r++) {
      for ($yy = $by - $r; $yy -le $by + $r -and -not $found; $yy++) {
        for ($xx = $bx - $r; $xx -le $bx + $r -and -not $found; $xx++) {
          if ($xx -lt 0 -or $yy -lt 0 -or $xx -ge $winW -or $yy -ge $winH) { continue }
          if ((HexAt $bmp $xx $yy) -eq $expect) { $found = "$xx,$yy" }
        }
      }
    }
    if ($found) { $note = " FALLBACK_WITHIN_8PX=$found" } else { $note = ' FALLBACK=NOT_FOUND_WITHIN_8PX'; [void]$script:misses.Add($name) }
  }
  Say ("POINT$tag $name client=($cx,$cy) bitmap=($bx,$by) rgb=$got expect=$expect HIT=$hit$note")
}
Probe 'RailBg#202020' 140 20 '#202020'
Probe 'PageBg#222222' 500 20 '#222222'
$searchCx = [int](256 + ($clientW - 256) / 2) + 50
Say ("CONTROL-POINT rule: clientW=$clientW contentColumn=256..$($clientW-1) searchBoxCentreX=$([int](256 + ($clientW - 256) / 2)) probedAt=($searchCx,20)")
Probe 'ControlBg#2F2F2F' $searchCx 20 '#2F2F2F'
Probe 'RowSelectedBg#2D2D2D' 140 265 '#2D2D2D'
Probe 'Accent#9D82C2' 2 265 '#9D82C2'

# ============ UI Automation dump: rects for the rendered strings (no input driven) ============
Say ''
Say '=== UI Automation dump (read-only) ==='
$uiaEls = New-Object System.Collections.ArrayList
try {
  Add-Type -AssemblyName UIAutomationClient
  Add-Type -AssemblyName UIAutomationTypes
  $rootEl = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
  $cond = [System.Windows.Automation.Condition]::TrueCondition
  $all = $rootEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
  Say ("UIA descendants=" + $all.Count)
  $n = 0
  foreach ($e in $all) {
    $n++
    try {
      $ct = $e.Current.ControlType.ProgrammaticName
      $nm = $e.Current.Name
      $r = $e.Current.BoundingRectangle
      if (-not [string]::IsNullOrEmpty($nm)) {
        Say ("UIA #$n type=$ct name='$nm' screen=$([int]$r.X),$([int]$r.Y),$([int]$r.Width)x$([int]$r.Height)")
      }
      [void]$uiaEls.Add([pscustomobject]@{
        Handle = $e
        Type = $ct
        Name = $nm
        X = [int]$r.X; Y = [int]$r.Y; W = [int]$r.Width; H = [int]$r.Height
      })
    } catch { }
  }
} catch {
  Say ("UIA FAILED " + $_.Exception.Message)
}

# ============ (4)/(5) row name colour + age line, driven by the UIA rectangles ============
Say ''
Say '=== CRITERION: failing name #FFADB6 vs normal name white / age line real value or placeholder ==='
Say 'RULE: take each UIA Text rectangle that lies inside the rail column (bitmap x < railEdge),'
Say '      convert screen -> bitmap (screenX - winLeft, screenY - winTop) and histogram the'
Say '      pixels of that rectangle; the dominant non-background colours are the rendered colour.'
$railEdge = 264
function HistRect([string]$label, [int]$bx, [int]$by, [int]$bw, [int]$bh) {
  if ($bw -le 0 -or $bh -le 0) { Say ("UIA-PIXEL $label empty rect"); return }
  $h = @{}
  for ($y = $by; $y -lt $by + $bh; $y++) {
    for ($x = $bx; $x -lt $bx + $bw; $x++) {
      if ($x -lt 0 -or $y -lt 0 -or $x -ge $winW -or $y -ge $winH) { continue }
      $k = HexAt $bmp $x $y
      if (-not $h.ContainsKey($k)) { $h[$k] = 0 }
      $h[$k] = $h[$k] + 1
    }
  }
  $top = $h.GetEnumerator() | Sort-Object { - $_.Value } | Select-Object -First 5
  Say ("UIA-PIXEL $label bitmap=$bx,$by,${bw}x${bh} colours=" + (($top | ForEach-Object { "$($_.Key)x$($_.Value)" }) -join ' '))
}
$textEls = @($uiaEls | Where-Object { $_.Type -eq 'ControlType.Text' -and ($_.X - $wr.Left) -lt $railEdge -and $_.Y -ge ($wr.Top + 200) })
Say ("UIA-RAIL-TEXTS count=" + $textEls.Count + "  (rail column only, below y=200 screen)")
foreach ($t in $textEls) {
  HistRect ("text='" + $t.Name + "'") ($t.X - $wr.Left) ($t.Y - $wr.Top) $t.W $t.H
}

# ============ status squares: coarse verification that colour follows the probe result ============
Say ''
Say 'RULE: status squares are 28 px tall and rows are 52 px apart; scan the column through the'
Say '      square centre (client x=27) for runs of ServerOk #50B649 / ServerFail #FFADB6.'
$sqx = 27 + $dx
$bands = New-Object System.Collections.ArrayList
$cur = ''
$start = -1
for ($by2 = 31; $by2 -lt $winH; $by2++) {
  $h = HexAt $bmp $sqx $by2
  $isSq = ($h -eq '#50B649' -or $h -eq '#FFADB6')
  if ($isSq) {
    if ($cur -eq '') { $cur = $h; $start = $by2 }
    elseif ($h -ne $cur) { [void]$bands.Add("$cur $start..$($by2-1)"); $cur = $h; $start = $by2 }
  }
  elseif ($cur -ne '') { [void]$bands.Add("$cur $start..$($by2-1)"); $cur = ''; $start = -1 }
}
Say ("STATUS-SQUARES col=bitmap x=$sqx (client 27): " + ($bands -join ' | '))
Say ("STATUS-SQUARES count=" + $bands.Count + " (ServerOk #50B649 = probe OK, ServerFail #FFADB6 = probe FAILED)")

# ============ ADDENDUM (captain 2026-09-11): RequestedTheme=Dark + read-only drag handle ============
Say ''
Say '=== ADDENDUM: dark theme nailed at the root + read-only rows grey their drag handle ==='
Say 'RULE (a): the page body card was #BDBDBD under the light system theme (CardBackgroundFillColorDefault'
Say '           over #222222). With RequestedTheme=Dark on the root it must no longer be #BDBDBD.'
$bodyGot = HexAt $bmp (700 + $dx) (400 + $dy)
$bodyOk = ($bodyGot -ne '#BDBDBD')
Say ("ADDENDUM-THEME bodySample client=(700,400) bitmap=(701,431) rgb=$bodyGot wasLightCard=(#BDBDBD) => " + $(if ($bodyOk) { 'DARK-THEME-OK' } else { 'STILL-LIGHT-FAIL' }))
Say 'RULE (b): each "=" drag handle is a TextBlock; its UIA rectangle histogram must show the disabled'
Say '           token #6A6A6A on read-only rows (from original accounts.json) and #9E9E9E on writable rows.'
$handles = @($uiaEls | Where-Object { $_.Type -eq 'ControlType.Text' -and $_.Name -eq '=' })
Say ("ADDENDUM-HANDLES count=" + $handles.Count)
foreach ($h in $handles) {
  HistRect "handle='=' y=$($h.Y - $wr.Top)" ($h.X - $wr.Left) ($h.Y - $wr.Top) $h.W $h.H
}

# ============ selection fallback: an interloper may have cleared the row selection ============
if ($script:misses.Count -gt 0) {
  Say ''
  Say ("SELECTION-FALLBACK needed, missed=" + ($script:misses -join ','))
  Say 'RULE: the app sets ServerList.SelectedIndex = 0 at startup; if a concurrent click cleared it,'
  Say '      re-assert it through the UI Automation SelectionItemPattern (no mouse/keyboard synthesis)'
  Say '      and re-capture; results below are tagged AFTER-RESELECT.'
  try {
    $liCond = New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::ListItem)
    $items = $rootEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, $liCond)
    $target = $null
    foreach ($it in $items) {
      if ($it.Current.Name -like '*ServerRow*') { $target = $it; break }
    }
    if ($null -ne $target) {
      $pat = $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
      $pat.Select()
      Say ("SELECTION-FALLBACK selected item '" + $target.Current.Name + "'")
      Start-Sleep -Milliseconds 900
      $bmp.Dispose()
      $bmp = Capture '-after-reselect'
      Probe 'RailBg#202020' 140 20 '#202020' '-AFTER'
      Probe 'PageBg#222222' 500 20 '#222222' '-AFTER'
      Probe 'ControlBg#2F2F2F' $searchCx 20 '#2F2F2F' '-AFTER'
      Probe 'RowSelectedBg#2D2D2D' 140 265 '#2D2D2D' '-AFTER'
      Probe 'Accent#9D82C2' 2 265 '#9D82C2' '-AFTER'
    } else {
      Say 'SELECTION-FALLBACK no ListItem matching *ServerRow* found'
    }
  } catch {
    Say ("SELECTION-FALLBACK FAILED " + $_.Exception.Message)
  }
}

# ============ app-side log lines ============
Say ''
Say '=== app log tail (shell-startup.log under the DATA ROOT, t146) ==='
# t146 moved the startup log out of the exe dir into `<data root>\logs\` (Program.cs PrepareLogSink ->
# AppDataDir.Instance.Root). Resolving it from $Exe read a stale legacy artifact (or nothing) and silently
# produced "APPLOG missing". Mirror AppDataDir's precedence EXACTLY, or the reading drifts from the app:
#   ① AIPLAYER_APPDATA_ROOT (explicit override)  ->  ② portable `<exe>\data`  ->  ③ %LOCALAPPDATA%\AIPlayer
$exeParent = Split-Path -Parent $Exe
if ($env:AIPLAYER_APPDATA_ROOT) { $appRoot = $env:AIPLAYER_APPDATA_ROOT }
elseif (Test-Path -LiteralPath (Join-Path $exeParent 'data')) { $appRoot = Join-Path $exeParent 'data' }
else { $appRoot = Join-Path $env:LOCALAPPDATA 'AIPlayer' }
$appLog = Join-Path (Join-Path $appRoot 'logs') 'shell-startup.log'
Say ("APPLOG-ROOT root=" + $appRoot + " path=" + $appLog + " exists=" + (Test-Path -LiteralPath $appLog))
if (Test-Path -LiteralPath $appLog) {
  # NOTE (captain 2026-09-11): the app log is UTF-8; PowerShell 5.1's Get-Content defaults to
  # the ANSI code page, which turned Chinese server names into mojibake in this very evidence file
  # ((反控)不可达服务器 read back as 鍙嶆帶...). Always decode explicitly.
  $tail = [System.IO.File]::ReadAllLines($appLog, [System.Text.Encoding]::UTF8) | Select-Object -Last 40
  foreach ($t in $tail) { Say ("APPLOG " + $t) }
  Say '--- app log: read-only rows and their handle tooltip (evidence for the addendum) ---'
  $rows = [System.IO.File]::ReadAllLines($appLog, [System.Text.Encoding]::UTF8) |
    Select-String -Pattern 'RAIL row |RAIL-CONTEXT' | Select-Object -Last 20
  if ($rows.Count -eq 0) { Say 'APPLOG-GREP RAIL row / RAIL-CONTEXT = 0 hits (check是否被其它进程轮转/截断)' }
  foreach ($t in $rows) { Say ("APPLOG-ROW " + $t.Line) }
} else {
  Say ("APPLOG missing path=" + $appLog)
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Say ("KILLED pid=" + $proc.Id)
Say ("EVIDENCE RUN t26 end=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
$bmp.Dispose()
$bmp2.Dispose()
Flush
Write-Host ("LOGFILE=" + $logPath)
exit 0
