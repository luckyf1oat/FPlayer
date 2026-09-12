# popup-capture-target-worker.ps1 -- test TARGET for popup-capture-focus-regression.ps1 (t47 follow-up).
#
# Modes:
#   -Mode form   : shows a NORMAL WinForms window (foregroundable -- used to inject a focus change into the
#                  capture tool's observation window; a light-dismiss popup cannot be foregrounded at all).
#   -Mode popup  : shows a light-dismiss-style popup (ContextMenuStrip, AutoClose=$false) -- the real flyout shape.
#
# It writes the HWND to capture to -HwndFile, then stays alive until -HoldMs elapses.
# Nothing here is product code. ASCII-only on purpose.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$HwndFile,
    [int]$HoldMs = 20000,
    [string]$Mode = 'popup',
    [int]$FormWidth = 360,
    [int]$FormHeight = 140
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$sig = @'
using System; using System.Text; using System.Runtime.InteropServices;
public class W46 {
    public delegate bool Proc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(Proc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
'@
[void](Add-Type -TypeDefinition $sig -Language CSharp)

$form = New-Object System.Windows.Forms.Form
$form.Text = 't47 worker form'
$form.StartPosition = 'Manual'
$form.SetBounds(320, 320, $FormWidth, $FormHeight)
$form.Show()

$menu = $null
if ($Mode -eq 'popup') {
    $menu = New-Object System.Windows.Forms.ContextMenuStrip
    $menu.AutoClose = $false
    [void]$menu.Items.Add('t47 focus-test item 1')
    [void]$menu.Items.Add('t47 focus-test item 2')
    $menu.Show(240, 240)
}
Start-Sleep -Milliseconds 700

$hwndToReport = 0
if ($Mode -eq 'form') {
    $hwndToReport = [long]$form.Handle
}
else {
    # popup: smallest WindowsForms10 window that is NOT the worker form itself
    $formHandle = [long]$form.Handle
    $best = 0
    $bestArea = [int]::MaxValue
    $cb = [W46+Proc]{
        param($h, $l)
        $wp = 0
        [void][W46]::GetWindowThreadProcessId($h, [ref]$wp)
        if ($wp -eq $PID -and [W46]::IsWindowVisible($h) -and ([long]$h -ne $formHandle)) {
            $sb = New-Object System.Text.StringBuilder 256
            [void][W46]::GetClassNameW($h, $sb, 256)
            if ($sb.ToString() -like 'WindowsForms10*') {
                $r = New-Object W46+RECT
                [void][W46]::GetWindowRect($h, [ref]$r)
                $a = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
                if ($a -gt 2000 -and $a -lt 400000 -and $a -lt $bestArea) { $script:bestArea = $a; $script:best = [long]$h }
            }
        }
        return $true
    }
    [void][W46]::EnumWindows($cb, [IntPtr]::Zero)
    $hwndToReport = $best
}

[System.IO.File]::WriteAllText($HwndFile, ([string]$hwndToReport), (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("WORKER|mode=" + $Mode + "|pid=" + $PID + "|hwnd=" + $hwndToReport + "|form=" + $form.Handle)

$deadline = (Get-Date).AddMilliseconds($HoldMs)
while ((Get-Date) -lt $deadline) {
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 100
}
if ($menu -ne $null) { $menu.Close() }
$form.Close()
Write-Output "WORKER|done"
