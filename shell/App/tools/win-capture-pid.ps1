# Capture the top-level window of an ALREADY RUNNING process (by pid) with PrintWindow.
# Needed for the playback chain: the shell (AIPlayer.Shell.exe) spawns the kernel
# (AIPlayer.MpvHost.exe) as a separate process, so win-capture.ps1 (which launches its own
# target) cannot reach the kernel window.
# ASCII-only on purpose (avoids the UTF-8/BOM PowerShell decoding trap).
# NOTE: the parameter must NOT be named $Pid -- that is a READ-ONLY automatic variable in
# PowerShell ("Cannot overwrite variable Pid because it is read-only or constant",
# measured 2026-09-11). Same family of traps as powershell.exe's own -File switch.
param(
  [Parameter(Mandatory=$true)][int]$TargetPid,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [string]$NamePrefix = 'kernelwin'
)

$ErrorActionPreference = 'Stop'
$script:OwnerPid = $TargetPid

$sig = @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class W2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Add-Type -TypeDefinition $sig -Language CSharp
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$best = [IntPtr]::Zero
$bestArea = 0
$bestTitle = ''
$bestClass = ''

$cb = [W2+EnumProc]{
  param($h, $l)
  $owner = 0
  [void][W2]::GetWindowThreadProcessId($h, [ref]$owner)
  if ($owner -ne $script:OwnerPid) { return $true }
  if (-not [W2]::IsWindowVisible($h)) { return $true }
  $r = New-Object W2+RECT
  [void][W2]::GetWindowRect($h, [ref]$r)
  $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
  if ($area -gt $script:bestArea) {
    $script:bestArea = $area
    $script:best = $h
    $sb = New-Object System.Text.StringBuilder 512
    [void][W2]::GetWindowTextW($h, $sb, 512)
    $script:bestTitle = $sb.ToString()
    $sb2 = New-Object System.Text.StringBuilder 512
    [void][W2]::GetClassNameW($h, $sb2, 512)
    $script:bestClass = $sb2.ToString()
  }
  return $true
}

[void][W2]::EnumWindows($cb, [IntPtr]::Zero)

if ($best -eq [IntPtr]::Zero) {
  Write-Host "no visible top-level window for pid $TargetPid"
  exit 2
}

$rect = New-Object W2+RECT
[void][W2]::GetWindowRect($best, [ref]$rect)
$w = $rect.Right - $rect.Left
$h2 = $rect.Bottom - $rect.Top
Write-Host "pid=$TargetPid hwnd=$best class='$bestClass' title='$bestTitle' size=${w}x${h2}"

# ===== H1 capture guards (allow-list entry: shell/tools/privacy-allowlist.txt) =====
# Guard 1: a desktop-class window is never ours, so a screen copy of it is always a privacy leak.
if ($bestClass -match '^(Progman|WorkerW|Shell_TrayWnd)$') {
  Write-Host "REFUSED desktop-class window class='$bestClass'; nothing written"
  exit 6
}
# Guard 2: a rect covering >=95% of the virtual desktop is a de-facto full-screen grab.
$sm = Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);' -Name CapSM -Namespace Cap -PassThru
# NOTE (verifier 2026-09-12): the type is NAMESPACED -- Add-Type -Namespace Cap -Name CapSM creates "Cap.CapSM",
# so a bare [CapSM] throws "Unable to find type [CapSM]" (kernel2 measured this in a powershell -File subhost).
# Use the -PassThru object (or the qualified name [Cap.CapSM]); both resolve in a -File subhost too.
$vsw = $sm::GetSystemMetrics(78); $vsh = $sm::GetSystemMetrics(79)
if ($vsh -gt 0) {
  $cover = ($w * $h2) / [double]($vsw * $vsh)
  if ($cover -ge 0.95) {
    Write-Host ("REFUSED rect covers {0:P1} of the virtual desktop ({1}x{2}); nothing written" -f $cover, $vsw, $vsh)
    exit 6
  }
}
# Guard 3 (pid ownership) is enforced while picking the HWND: GetWindowThreadProcessId must equal $script:OwnerPid.
# Guard 4: the written provenance, so every PNG can be traced back to one window.
Write-Host ("SOURCE-ASSERTION hwnd=$best pid=$TargetPid rect=$($rect.Left),$($rect.Top),$($rect.Right),$($rect.Bottom) size=${w}x${h2} rect-source=foreground-window")

[void][W2]::SetForegroundWindow($best)
Start-Sleep -Milliseconds 700

$bmp = New-Object System.Drawing.Bitmap $w, $h2
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
# flags = 2 = PW_RENDERFULLCONTENT. Measured 2026-09-12 16:35: flags = 0 comes back BLANK on this
# WinUI 3 / DirectComposition window (9,824 B for a 1440x759 frame = one flat colour) while the same
# window through t26-evidence.ps1 (flags = 2) is 901 KB of real pixels. Do not pass 0 here again.
$ok = [W2]::PrintWindow($best, $hdc, 2)
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()

$out = Join-Path $OutDir ($NamePrefix + '-printwindow.png')
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$len = (Get-Item -LiteralPath $out).Length

# Blank detector: a composited window that did NOT present a frame comes back as one flat colour.
# Sample a 9x9 grid and count distinct colours; <= 2 distinct is treated as BLANK (and is reported
# as a FAILURE of this channel, not passed off as an image of the UI).
$distinct = New-Object 'System.Collections.Generic.HashSet[string]'
for ($gy = 0; $gy -lt 9; $gy++) {
  for ($gx = 0; $gx -lt 9; $gx++) {
    $px = [int](($gx + 0.5) * $w / 9.0)
    $py = [int](($gy + 0.5) * $h2 / 9.0)
    if ($px -ge $w) { $px = $w - 1 }
    if ($py -ge $h2) { $py = $h2 - 1 }
    $c = $bmp.GetPixel($px, $py)
    [void]$distinct.Add(('#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B))
  }
}
$bmp.Dispose()

Write-Host "printwindow ok=$ok -> $out bytes=$len size=${w}x${h2} distinct9x9=$($distinct.Count) title='$bestTitle'"
if ($distinct.Count -le 2) {
  Write-Host "BLANK-SUSPECT printwindow distinct9x9=$($distinct.Count) bytes=$len size=${w}x${h2} (one flat colour = no presented frame)"
}

# ===== CopyFromScreen channel (captain 2026-09-11) =====
# D3D composited surfaces often return a black frame via PrintWindow (measured 9,757 B internally),
# so the window must be brought forward and the screen region copied, then verified by eye.
$w3 = Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd); [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);' -Name CapW3 -Namespace Cap -PassThru

[void]$w3::ShowWindow($best, 9)                                     # SW_RESTORE
[void]$w3::SetWindowPos($best, [IntPtr](-1), 0, 0, 0, 0, 0x0043)    # TOPMOST|NOSIZE|NOMOVE|SHOWWINDOW
[void][W2]::SetForegroundWindow($best)
Start-Sleep -Milliseconds 1500                                      # let the composited surface present a frame

# Re-read the rect, but CAPTURE with the rect that the source assertion above described. Measured
# 2026-09-12 16:35: the re-read rect came back 160x28 (the window was still settling) and the whole
# screenshot was therefore a 148-byte sliver that looked like a capture but contained no UI.
$rect2 = New-Object W2+RECT
[void][W2]::GetWindowRect($best, [ref]$rect2)
$w3s = $rect2.Right - $rect2.Left
$h3s = $rect2.Bottom - $rect2.Top
if ($w3s -ne $w -or $h3s -ne $h2) {
  Write-Host "RECT-DRIFT first=${w}x${h2} second=${w3s}x${h3s} (capturing the FIRST rect, the one under SOURCE-ASSERTION)"
}
$bmp2 = New-Object System.Drawing.Bitmap $w, $h2
$gfx2 = [System.Drawing.Graphics]::FromImage($bmp2)
$gfx2.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h2))
$gfx2.Dispose()
$out2 = Join-Path $OutDir ($NamePrefix + '-screencapture.png')
$bmp2.Save($out2, [System.Drawing.Imaging.ImageFormat]::Png)
$len2 = (Get-Item -LiteralPath $out2).Length
$bmp2.Dispose()

$ratio = [math]::Round($len2 / [double]($w * $h2), 4)
Write-Host "screencapture -> $out2 bytes=$len2 size=${w}x${h2} bytesPerPixel=$ratio title='$bestTitle'"
if ($ratio -lt 0.02) {
  Write-Host "BLANK-SUSPECT screencapture bytesPerPixel=$ratio (a real frame of this UI measures >= 0.5)"
}
exit 0
