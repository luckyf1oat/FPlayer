# Chain the kernel's XamlTypeInfo into the shell's generated metadata-provider list.
#
# Why this file exists (measured 2026-09-11, not inferred):
#   The WinUI XamlCompiler generates obj\...\XamlTypeInfo.g.cs. Its OtherProviders block only
#   chains XamlMetaDataProvider types coming from OTHER assemblies (XamlControls, Sizers,
#   Richasy.MpvKernel.WinUI, Richasy.WinUIKernel.Share, WinUIEx). The decompiled kernel's
#   WinUISample.AIPlayer_MpvHost_XamlTypeInfo provider is compiled into THIS assembly, so the
#   generator never lists it => kernel XBF parsing cannot resolve kernel types =>
#   TARGET-FAIL XamlParseException (HRESULT 0x802B000A), while a direct
#   kernel GetXamlTypeByName("WinUISample.Controls.PlayerOverlayBase") does return a type.
#
# Inserts ONE pair of lines right after the LAST "otherProviders.Add(provider);" occurrence.
# Idempotent (marker = KernelXamlMetaDataProvider) and FAILS LOUDLY when the anchor is missing,
# so a toolchain change can never silently drop the chain and yield a runtime-only failure.
#
# Implementation notes (both learned the hard way here):
#   * the parameter must NOT be named -File / -Command / -NoProfile: "powershell -File <script>
#     -File <x>" makes powershell.exe swallow the second switch as its own (MSB3073, silent).
#   * use String.Insert on the whole text, NOT $lines[a..b]: PowerShell 5.1 array slicing yields
#     Object[], which List[string].AddRange rejects.
# ASCII-only on purpose (avoids the UTF-8/BOM PowerShell decoding trap).

param(
  [Parameter(Mandatory=$true)][string]$Target,
  [string]$Marker = 'KernelXamlMetaDataProvider',
  [string]$Anchor = 'otherProviders.Add(provider);'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Target)) {
  Write-Error "chain-kernel-xamltinfo: target file not found: $Target"
  exit 1
}

$text = [System.IO.File]::ReadAllText($Target)

if ($text.IndexOf($Marker) -ge 0) {
  Write-Host "chain-kernel-xamltinfo: already chained; no change."
  exit 0
}

$last = $text.LastIndexOf($Anchor)
if ($last -lt 0) {
  Write-Error "chain-kernel-xamltinfo: anchor '$Anchor' not found in $Target - the generated XamlTypeInfo layout changed; refusing to patch silently."
  exit 1
}

$inject = "`r`n                    provider = new global::AIPlayer.Shell.KernelHost.KernelXamlMetaDataProvider() as global::Microsoft.UI.Xaml.Markup.IXamlMetadataProvider; // $Marker" +
          "`r`n                    otherProviders.Add(provider); "

$index = $last + $Anchor.Length
$text = $text.Insert($index, $inject)

[System.IO.File]::WriteAllText($Target, $text, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "chain-kernel-xamltinfo: patched at offset $index ($Target)."
exit 0
