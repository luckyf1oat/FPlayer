# popup-capture-demo.ps1 -- self-contained, reproducible A1 harness for popup-capture.ps1.
#
# It opens a real light-dismiss-style popup in THIS process (a WinForms ContextMenuStrip, which is a
# ToolStripDropDown: separate popup HWND, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE => showing it does NOT steal
# the foreground, which is exactly the property that makes MenuFlyout evidence possible), then asks
# popup-capture.ps1 to capture that HWND, prints the tool's raw output, and closes the popup.
#
# Nothing here touches product code. The PNG and its .txt sidecar land wherever -Out says.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture-demo.ps1 -Out %TEMP%\popup.png
#
# ASCII-only on purpose.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Tool = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if ($Tool -eq '') { $Tool = Join-Path $PSScriptRoot 'popup-capture.ps1' }
if (-not (Test-Path -LiteralPath $Tool)) { Write-Output ("ERROR|tool not found: " + $Tool); exit 2 }

$menu = New-Object System.Windows.Forms.ContextMenuStrip
[void]$menu.Items.Add('Popup evidence sample 1')
[void]$menu.Items.Add('Popup evidence sample 2')
[void]$menu.Items.Add('Popup evidence sample 3')
$menu.Items[0].BackColor = [System.Drawing.Color]::FromArgb(0x9D, 0x82, 0xC2)

# Show at a fixed on-screen point WITHOUT activating (ToolStripDropDown is WS_EX_NOACTIVATE).
$menu.Show(120, 160)
Start-Sleep -Milliseconds 700

# Give the popup HWND to the capture tool explicitly: enumerate this process and pick the tool-strip window.
$listOut = & powershell -NoProfile -ExecutionPolicy Bypass -File $Tool -Pid $PID -List 2>&1
$hwnd = 0
$cls = ''
foreach ($line in $listOut) {
    $s = [string]$line
    if ($s.StartsWith('CAND|') -and $s -match 'class=WindowsForms10') {
        if ($s -match 'hwnd=(\d+)') { $hwnd = [long]$Matches[1] }
        if ($s -match 'class=([^|]+)') { $cls = $Matches[1] }
        break
    }
}
Write-Output ("DEMO|popup hwnd=" + $hwnd + " class=" + $cls + " pid=" + $PID)

if ($hwnd -le 0) {
    Write-Output "DEMO|FAIL no WindowsForms10 window found in this process"
    $menu.Close(); $menu.Dispose()
    exit 1
}

$capOut = & powershell -NoProfile -ExecutionPolicy Bypass -File $Tool -Pid $PID -Hwnd $hwnd -Out $Out 2>&1
$capCode = $LASTEXITCODE
foreach ($line in $capOut) { Write-Output ([string]$line) }
Write-Output ("DEMO|exit=" + $capCode)

$menu.Close()
$menu.Dispose()
exit $capCode
