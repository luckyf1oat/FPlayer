# ui-click-capture.ps1 -- foreground a window, optionally click a CLIENT-relative point, then capture.
# For collecting reference screenshots of a running third-party app (HillsLite) whose UI we are
# re-implementing in our own shell. Flutter windows expose almost nothing to UI Automation, so we
# drive them by absolute cursor position derived from the window rect.
#
# ASCII-only on purpose (PS 5.1 decodes BOM-less UTF-8 as ANSI -> a non-ASCII source CRASHES).
# Params must avoid PowerShell automatic variables and host switch names.
# ONE Add-Type block only: a second block cannot see types compiled into the first
# (measured 2026-09-11: CS0246 on the nested RECT type => capture silently produced nothing).
param(
  [Parameter(Mandatory=$true)][int]$TargetPid,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [Parameter(Mandatory=$true)][string]$NamePrefix,
  [int]$ClientX = -1,
  [int]$ClientY = -1,
  [int]$WheelTicks = 0,          # negative = scroll DOWN (3 lines per tick), positive = up
  [int]$WheelX = 700,            # where to park the cursor while wheeling (client coords)
  [int]$WheelY = 420,
  [int]$WaitMs = 1500,
  [switch]$NoClick
)

$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class Uic {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
}
"@

$proc = Get-Process -Id $TargetPid
$h = $proc.MainWindowHandle
if ($h -eq 0) { Write-Host "NO-WINDOW pid=$TargetPid"; exit 2 }

[Uic]::ShowWindow($h, 9) | Out-Null
[Uic]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 450

$cr = New-Object Uic+RECT
[Uic]::GetClientRect($h, [ref]$cr) | Out-Null
Write-Host ("CLIENT {0}x{1}" -f ($cr.R - $cr.L), ($cr.B - $cr.T))

if (-not $NoClick -and $ClientX -ge 0 -and $ClientY -ge 0) {
  $pt = New-Object System.Drawing.Point($ClientX, $ClientY)
  [Uic]::ClientToScreen($h, [ref]$pt) | Out-Null
  [Uic]::SetCursorPos($pt.X, $pt.Y) | Out-Null
  Start-Sleep -Milliseconds 180
  [Uic]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
  [Uic]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
  Write-Host ("CLICK client=({0},{1}) screen=({2},{3})" -f $ClientX, $ClientY, $pt.X, $pt.Y)
} else { Write-Host "CLICK skipped" }

if ($WheelTicks -ne 0) {
  $wp = New-Object System.Drawing.Point($WheelX, $WheelY)
  [Uic]::ClientToScreen($h, [ref]$wp) | Out-Null
  [Uic]::SetCursorPos($wp.X, $wp.Y) | Out-Null
  Start-Sleep -Milliseconds 180
  $delta = if ($WheelTicks -lt 0) { -120 } else { 120 }
  for ($i = 0; $i -lt [math]::Abs($WheelTicks); $i++) {
    [Uic]::mouse_event(0x0800, 0, 0, $delta, [IntPtr]::Zero)   # MOUSEEVENTF_WHEEL
    Start-Sleep -Milliseconds 70
  }
  Write-Host ("WHEEL ticks={0} delta={1} at client=({2},{3})" -f $WheelTicks, $delta, $WheelX, $WheelY)
}

Start-Sleep -Milliseconds $WaitMs

$r = New-Object Uic+RECT
[Uic]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.R - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [Uic]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$path = Join-Path $OutDir ($NamePrefix + '-printwindow.png')
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host ("PRINTWINDOW ok={0} size={1}x{2} bytes={3}" -f $ok, $w, $ht, (Get-Item $path).Length)
