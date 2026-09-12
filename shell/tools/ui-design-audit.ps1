# ui-design-audit.ps1 -- one-command UI design compliance audit for evidence screenshots.
#
# ASCII-only on purpose: PowerShell 5.1 decodes BOM-less UTF-8 scripts as ANSI, so any
# non-ASCII character in a .ps1 file can break parsing. All human-readable Chinese text
# belongs to README-ui-audit.md (UTF-8) and to the optional -Json output (UTF-8, no BOM).
#
# Four assertion groups (all required):
#   1) token   : sample UI_SPEC_SHELL.md section 1 tokens at fixed coordinates / regions
#   2) geometry: sidebar width (scan from x=0), chip height/width, poster/card rects
#   3) channel : -Channel x -WindowKind legality; PrintWindow + kernel => INCONCLUSIVE
#   4) stats   : full-pixel non-black ratio + content row count (no sampling steps allowed)
#
# Verdicts: PASS / FAIL / INCONCLUSIVE.  Exit code: 0 = no FAIL, 1 = at least one FAIL,
# 2 = usage / IO error (missing image, bad parameter). INCONCLUSIVE never crashes.
#
# Rules inherited from shell/docs/VERIFY_PLAN_T35_T41.md: R1-R4 (capture channel and window
# kind must be declared; kernel window needs CopyFromScreen; offline re-read of pixels),
# A0-4 (every data/player or evidence claim carries time + hash), A0-5 (screenshots carry
# channel + window kind + size + bytes + sha12 + non-black ratio).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Image,
    [string]$Channel = '',
    [string]$WindowKind = 'unknown',
    [string]$Origin = '0,0',
    [string]$TokenProbes = '',
    [string]$ReferenceSize = '1403x794',
    [string]$GeometryOriginSize = '',
    [int]$SidebarScanY = 200,
    [int]$SidebarExpected = 256,
    [int]$SidebarTolerance = 2,
    [int]$SidebarRunMin = 8,
    [int]$ChipScanY = 66,
    [int]$ChipProbeX = 380,
    [int]$ChipHeightExpected = 33,
    [int]$ChipHeightTolerance = 2,
    [string]$PosterRect = '',
    [string]$CardRects = '',
    [int]$Tolerance = 0,
    [double]$ExpectNonBlackMin = -1,
    [switch]$ForceGeometry,
    [string]$ContrastProbes = '',
    [switch]$SkipContrastStatic,
    [string]$XamlPath = '',
    [string]$XamlMustPass = '',
    [string]$XamlStylerExe = '',
    [string]$Json = ''
)

$ErrorActionPreference = 'Stop'

function Fail-Usage([string]$msg) {
    Write-Output ("ERROR|" + $msg)
    Write-Output ("USAGE|ui-design-audit.ps1 -Image <png> -Channel <printwindow|copyfromscreen|unknown> -WindowKind <xaml|kernel|unknown> [-Origin dx,dy] [-TokenProbes ...] [-Json out.json]")
    exit 2
}

function Parse-Hex([string]$hex) {
    $h = $hex.Trim()
    if ($h.StartsWith('#')) { $h = $h.Substring(1) }
    if ($h.Length -ne 6) { throw ("bad colour: " + $hex) }
    return @{
        R = [Convert]::ToInt32($h.Substring(0, 2), 16)
        G = [Convert]::ToInt32($h.Substring(2, 2), 16)
        B = [Convert]::ToInt32($h.Substring(4, 2), 16)
    }
}

function Parse-Pair([string]$s) {
    $p = $s.Split(',')
    if ($p.Length -ne 2) { throw ("bad pair: " + $s) }
    return @{ A = [int]$p[0]; B = [int]$p[1] }
}

# ---- load image into a raw BGRA buffer (one full scan, reused everywhere) ----
if (-not (Test-Path -LiteralPath $Image)) {
    Fail-Usage ("image not found: " + $Image)
}
$imgPath = (Resolve-Path -LiteralPath $Image).Path
$fi = Get-Item -LiteralPath $imgPath
$sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $imgPath).Hash.Substring(0, 12)
$sampleTime = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')

Add-Type -AssemblyName System.Drawing
$bmp = $null
try { $bmp = [System.Drawing.Bitmap]::FromFile($imgPath) } catch { Fail-Usage ("cannot decode png: " + $_.Exception.Message) }

$W = $bmp.Width
$H = $bmp.Height
$rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
$bd = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = [Math]::Abs($bd.Stride)
$buf = New-Object byte[] ($stride * $H)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $buf, 0, $buf.Length)
$bmp.UnlockBits($bd)
$bmp.Dispose()
$bmp = $null

function Get-Px([int]$x, [int]$y) {
    $i = $y * $stride + $x * 4
    return @{ B = $buf[$i]; G = $buf[$i + 1]; R = $buf[$i + 2] }
}

function Get-HexAt([int]$x, [int]$y) {
    $c = Get-Px $x $y
    return ("#{0:X2}{1:X2}{2:X2}" -f $c.R, $c.G, $c.B)
}

function Test-ColorEq($c1, $c2, [int]$tol) {
    if ([Math]::Abs($c1.R - $c2.R) -gt $tol) { return $false }
    if ([Math]::Abs($c1.G - $c2.G) -gt $tol) { return $false }
    if ([Math]::Abs($c1.B - $c2.B) -gt $tol) { return $false }
    return $true
}

# NOTE: do not assign back into a param variable whose type was declared ([string]$Origin):
# PowerShell keeps the type constraint and silently coerces the new value to string.
# Empirically: $origin = Parse-Pair $Origin left $origin as System.String and $origin.A as null.
$originPair = Parse-Pair $Origin
$originText = ("{0},{1}" -f $originPair.A, $originPair.B)
$rowsOut = New-Object System.Collections.ArrayList

function Add-Row([string]$group, [string]$item, [string]$verdict, [string]$measured, [string]$expected, [string]$note) {
    [void]$rowsOut.Add(@{ group = $group; item = $item; verdict = $verdict; measured = $measured; expected = $expected; note = $note })
}

# ================= 4) full-pixel statistics (no sampling steps anywhere) =================
# non-black := any channel > 12 ; content row := row with at least one non-black pixel.
$nonBlack = 0
$whitePx = 0
$contentRows = 0
for ($y = 0; $y -lt $H; $y++) {
    $rowNonBlack = 0
    $rowBase = $y * $stride
    for ($x = 0; $x -lt $W; $x++) {
        $i = $rowBase + $x * 4
        $b = $buf[$i]; $g = $buf[$i + 1]; $r = $buf[$i + 2]
        if ($r -gt 12 -or $g -gt 12 -or $b -gt 12) {
            $rowNonBlack++
            if ($r -ge 250 -and $g -ge 250 -and $b -ge 250) { $whitePx++ }
        }
    }
    $nonBlack += $rowNonBlack
    if ($rowNonBlack -gt 0) { $contentRows++ }
}
$totalPx = $W * $H
$nonBlackPct3 = [Math]::Round(100.0 * $nonBlack / $totalPx, 3)
$whitePct3 = [Math]::Round(100.0 * $whitePx / $totalPx, 3)

$channelText = '<none>'
if ($Channel -ne '') { $channelText = $Channel }
Write-Output ("AUDIT|file=" + $Image + "|bytes=" + $fi.Length + "|sha256_12=" + $sha + "|mtime=" + $fi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') + "|size=" + $W + "x" + $H + "|channel=" + $channelText + "|windowkind=" + $WindowKind + "|origin=" + $originText + "|sampled=" + $sampleTime)

$statsVerdict = 'PASS'
$statsNote = 'full-pixel scan, no sampling step'
if ($ExpectNonBlackMin -ge 0) {
    if ($nonBlackPct3 -lt $ExpectNonBlackMin) {
        $statsVerdict = 'FAIL'
        $statsNote = ("non-black below -ExpectNonBlackMin " + $ExpectNonBlackMin)
    }
}
Add-Row 'stats' 'NonBlackPct' $statsVerdict ("{0}% ({1}/{2})" -f $nonBlackPct3, $nonBlack, $totalPx) 'informational (or >= -ExpectNonBlackMin)' $statsNote
Add-Row 'stats' 'WhitePct' 'PASS' ("{0}% ({1})" -f $whitePct3, $whitePx) 'informational' 'pure white r,g,b >= 250'
Add-Row 'stats' 'ContentRows' 'PASS' ("{0}" -f $contentRows) 'informational' 'rows with >=1 non-black pixel'

# ================= 3) channel compliance =================
$ch = $Channel.ToLowerInvariant()
$wk = $WindowKind.ToLowerInvariant()
if ($ch -eq '') {
    Add-Row 'channel' 'ChannelDeclared' 'INCONCLUSIVE' 'channel=<none>' 'printwindow|copyfromscreen' 'channel not provided => the screenshot must NOT be used as rendering evidence (R1)'
}
elseif ($ch -eq 'printwindow' -and $wk -eq 'kernel') {
    $uni = 'not uniform'
    if ($nonBlackPct3 -le 0.001) { $uni = 'all black' }
    elseif ($whitePct3 -ge 99.9) { $uni = 'all white' }
    Add-Row 'channel' 'ChannelWindowPair' 'INCONCLUSIVE' ("printwindow + kernel (" + $uni + ", nonblack=" + $nonBlackPct3 + "%)") 'copyfromscreen for kernel windows' 'R2: PrintWindow on a kernel mixed-composition window yields a white/black frame => evidence invalid, NOT a rendering failure'
}
elseif ($ch -eq 'printwindow' -and $wk -eq 'xaml') {
    Add-Row 'channel' 'ChannelWindowPair' 'PASS' 'printwindow + xaml' 'printwindow + xaml' 'R2: PrintWindow is valid for pure XAML windows'
}
elseif ($ch -eq 'copyfromscreen' -and ($wk -eq 'xaml' -or $wk -eq 'kernel')) {
    Add-Row 'channel' 'ChannelWindowPair' 'PASS' ("copyfromscreen + " + $wk) 'copyfromscreen + xaml|kernel' 'R2: screen copy is valid for both window kinds'
}
else {
    Add-Row 'channel' 'ChannelWindowPair' 'INCONCLUSIVE' ("channel=" + $ch + " windowkind=" + $wk) 'printwindow|copyfromscreen + xaml|kernel' 'unknown channel or window kind => cannot judge admissibility of this screenshot'
}

# ================= 1) token probes =================
$defaultProbes = @(
    'RailBg=#202020@100,300,pixel',
    'PageBg=#222222@1000,20,pixel',
    'SurfaceBg=#282828@1000,200,pixel',
    'ControlBg=#2F2F2F@700,20,pixel',
    'ControlBorderSearch=#3E3E3E@666,20,majority,5,1',
    'ControlBorderChip=#434343@264,68,pixel',
    'RowSelectedBg=#2D2D2D@120,410,pixel',
    'Accent=#9D82C2@380,66,pixel',
    'TextPrimary=#FFFFFF@264,56,present,38,33',
    'TextSecondary=#9E9E9E-#B5B5B5@233,410,present,12,1',
    'ServerOk=#50B649@16,410,present,28,28',
    'ServerFail=#FFADB6@0,0,unpinned'
) -join ';'

$probeText = $TokenProbes
$usingDefault = $false
if ($probeText -eq '') { $probeText = $defaultProbes; $usingDefault = $true }

$ref = Parse-Pair ($ReferenceSize.ToLowerInvariant().Replace('x', ','))
$sizeMatchesRef = ($W -eq $ref.A -and $H -eq $ref.B)
if ($GeometryOriginSize -ne '') { $ref = Parse-Pair ($GeometryOriginSize.ToLowerInvariant().Replace('x', ',')) ; $sizeMatchesRef = ($W -eq $ref.A -and $H -eq $ref.B) }

$probeItems = $probeText.Split(';')
foreach ($raw in $probeItems) {
    $p = $raw.Trim()
    if ($p -eq '') { continue }
    $eq = $p.IndexOf('=')
    if ($eq -lt 1) { Add-Row 'token' $p 'INCONCLUSIVE' 'unparseable probe' 'name=#RRGGBB@x,y,kind[,w,h]' 'probe grammar error'; continue }
    $name = $p.Substring(0, $eq)
    $rest = $p.Substring($eq + 1)
    $at = $rest.IndexOf('@')
    if ($at -lt 1) { Add-Row 'token' $name 'INCONCLUSIVE' 'unparseable probe' 'name=#RRGGBB@x,y,kind[,w,h]' 'probe grammar error'; continue }
    $colorText = $rest.Substring(0, $at)
    $tail = $rest.Substring($at + 1)
    $parts = $tail.Split(',')
    if ($parts.Length -lt 3) { Add-Row 'token' $name 'INCONCLUSIVE' 'unparseable probe' 'name=#RRGGBB@x,y,kind[,w,h]' 'probe grammar error'; continue }
    $kind = $parts[2].ToLowerInvariant()
    if ($kind -eq 'unpinned') {
        Add-Row 'token' $name 'INCONCLUSIVE' ('expected=' + $colorText + ' @ unpinned') $colorText 'sampling point not pinned in UI_SPEC_SHELL section 1 (ServerFail source image differs)'
        continue
    }
    $px = [int]$parts[0]
    $py = [int]$parts[1]
    $rw = 1
    $rh = 1
    if ($parts.Length -ge 5) { $rw = [int]$parts[3]; $rh = [int]$parts[4] }
    $ax = $px + $origin.A
    $ay = $py + $origin.B

    $rangeHi = $null
    $expHex = $colorText
    if ($colorText.Contains('-')) {
        $two = $colorText.Split('-')
        $expHex = $two[0]
        $rangeHi = $two[1]
    }
    $exp = Parse-Hex $expHex

    if ($usingDefault -and (-not $sizeMatchesRef) -and (-not $ForceGeometry)) {
        Add-Row 'token' $name 'INCONCLUSIVE' ('not judged @ (' + $ax + ',' + $ay + ')') ("{0} @ ({1},{2})" -f $colorText, $px, $py) ("probe set reference " + $ref.A + "x" + $ref.B + " != image " + $W + "x" + $H + "; pass -TokenProbes for this layout")
        continue
    }

    if ($ax -lt 0 -or $ay -lt 0 -or ($ax + $rw) -gt $W -or ($ay + $rh) -gt $H) {
        Add-Row 'token' $name 'FAIL' ("out of bounds @ (" + $ax + "," + $ay + ")") ("{0} @ ({1},{2})" -f $colorText, $px, $py) 'probe rectangle exceeds image bounds'
        continue
    }

    $verdict = 'MISS'
    $measured = ''
    if ($kind -eq 'pixel') {
        $measured = Get-HexAt $ax $ay
        if (Test-ColorEq (Get-Px $ax $ay) $exp $Tolerance) { $verdict = 'HIT' }
    }
    elseif ($kind -eq 'majority') {
        $counts = @{}
        for ($yy = $ay; $yy -lt ($ay + $rh); $yy++) {
            for ($xx = $ax; $xx -lt ($ax + $rw); $xx++) {
                $h6 = Get-HexAt $xx $yy
                if ($counts.ContainsKey($h6)) { $counts[$h6] = $counts[$h6] + 1 } else { $counts[$h6] = 1 }
            }
        }
        $best = ''
        $bestN = -1
        foreach ($k in $counts.Keys) { if ($counts[$k] -gt $bestN) { $bestN = $counts[$k]; $best = $k } }
        $measured = ($best + " x" + $bestN + "/" + ($rw * $rh))
        if (Test-ColorEq (Parse-Hex $best) $exp $Tolerance) { $verdict = 'HIT' }
    }
    elseif ($kind -eq 'present') {
        $hitN = 0
        $firstHit = ''
        for ($yy = $ay; $yy -lt ($ay + $rh); $yy++) {
            for ($xx = $ax; $xx -lt ($ax + $rw); $xx++) {
                $c = Get-Px $xx $yy
                $ok = $false
                if ($rangeHi -ne $null) {
                    # range token (e.g. #9E9E9E-#B5B5B5): any component-wise value inside the range counts
                    $hi = Parse-Hex $rangeHi
                    $ok = ((($c.R -ge $exp.R) -and ($c.R -le $hi.R)) -and (($c.G -ge $exp.G) -and ($c.G -le $hi.G)) -and (($c.B -ge $exp.B) -and ($c.B -le $hi.B)))
                }
                else {
                    $ok = Test-ColorEq $c $exp $Tolerance
                }
                if ($ok) { $hitN++; if ($firstHit -eq '') { $firstHit = ("#{0:X2}{1:X2}{2:X2}" -f $c.R, $c.G, $c.B) + "@(" + $xx + "," + $yy + ")" } }
            }
        }
        $measured = ("present=" + $hitN + "/" + ($rw * $rh) + " first=" + $firstHit)
        if ($hitN -gt 0) { $verdict = 'HIT' }
    }
    else {
        Add-Row 'token' $name 'INCONCLUSIVE' ('unknown kind: ' + $kind) $colorText 'pixel|majority|present'
        continue
    }

    $vd = 'FAIL'
    if ($verdict -eq 'HIT') { $vd = 'PASS' }
    $expText = $colorText
    if ($rw -gt 1 -or $rh -gt 1) { $expText = $expText + (" @ (" + $px + "," + $py + ") " + $rw + "x" + $rh) } else { $expText = $expText + (" @ (" + $px + "," + $py + ")") }
    Add-Row 'token' $name $vd ("{0} kind={1}" -f $measured, $kind) ("{0} kind={1}" -f $expText, $kind) ('probe coords are reference-layout coordinates; +Origin ' + $originText + ' applied')
}

# ================= 2) geometry =================
$geoLocked = $usingDefault -and (-not $sizeMatchesRef) -and (-not $ForceGeometry)
$geoNote = ("geometry is layout-locked to the reference layout " + $ref.A + "x" + $ref.B + " != image " + $W + "x" + $H + "; pass -ForceGeometry to judge anyway")
if ($geoLocked) {
    Add-Row 'geometry' 'SidebarWidth' 'INCONCLUSIVE' 'not judged' ($SidebarExpected.ToString() + ' +- ' + $SidebarTolerance) $geoNote
    Add-Row 'geometry' 'ChipHeight' 'INCONCLUSIVE' 'not judged' ($ChipHeightExpected.ToString() + ' +- ' + $ChipHeightTolerance) $geoNote
    Add-Row 'geometry' 'ChipWidths' 'INCONCLUSIVE' 'not judged' '38 / 52 / 80 (+-2)' $geoNote
}
else {
    # sidebar: first run of >= SidebarRunMin consecutive PageBg pixels, scanning from x=0 on row SidebarScanY
    $page = Parse-Hex '#222222'
    $sx = -1
    if ($SidebarScanY -lt $H) {
        $run = 0
        for ($x = 0; $x -lt $W; $x++) {
            if (Test-ColorEq (Get-Px $x $SidebarScanY) $page $Tolerance) {
                $run++
                if ($run -ge $SidebarRunMin) { $sx = $x - $run + 1; break }
            }
            else { $run = 0 }
        }
    }
    if ($sx -lt 0) {
        Add-Row 'geometry' 'SidebarWidth' 'INCONCLUSIVE' ('no PageBg run >= ' + $SidebarRunMin + ' px on y=' + $SidebarScanY) ($SidebarExpected.ToString() + ' +- ' + $SidebarTolerance) 'cannot locate the content-area boundary by colour'
    }
    else {
        $ok = ([Math]::Abs($sx - $SidebarExpected) -le $SidebarTolerance)
        $vd = 'FAIL'; if ($ok) { $vd = 'PASS' }
        Add-Row 'geometry' 'SidebarWidth' $vd ($sx.ToString() + ' px (y=' + $SidebarScanY + ')') ($SidebarExpected.ToString() + ' +- ' + $SidebarTolerance) 'first run of >=8 consecutive PageBg #222222 from x=0'
    }

    # chip height: vertical run of Accent at ChipProbeX
    $accent = Parse-Hex '#9D82C2'
    $chTop = -1; $chBot = -1
    if ($ChipProbeX -lt $W) {
        for ($y = 0; $y -lt $H; $y++) {
            if (Test-ColorEq (Get-Px $ChipProbeX $y) $accent $Tolerance) { if ($chTop -lt 0) { $chTop = $y }; $chBot = $y }
        }
    }
    if ($chTop -lt 0) {
        Add-Row 'geometry' 'ChipHeight' 'INCONCLUSIVE' ('no Accent #9D82C2 pixel in column x=' + $ChipProbeX) ($ChipHeightExpected.ToString() + ' +- ' + $ChipHeightTolerance) 'selected-chip accent colour not present in that column'
    }
    else {
        $hh = $chBot - $chTop + 1
        $ok = ([Math]::Abs($hh - $ChipHeightExpected) -le $ChipHeightTolerance)
        $vd = 'FAIL'; if ($ok) { $vd = 'PASS' }
        Add-Row 'geometry' 'ChipHeight' $vd (($hh.ToString()) + ' px (column x=' + $ChipProbeX + ', y ' + $chTop + '..' + $chBot + ')') ($ChipHeightExpected.ToString() + ' +- ' + $ChipHeightTolerance) 'vertical run of Accent colour'
    }

    # chip widths: horizontal runs of ControlBg or Accent on ChipScanY, length >= 30
    $ctl = Parse-Hex '#2F2F2F'
    $runs = New-Object System.Collections.ArrayList
    if ($ChipScanY -lt $H) {
        $run = 0; $start = -1
        for ($x = 0; $x -lt $W; $x++) {
            $c = Get-Px $x $ChipScanY
            $isChip = (Test-ColorEq $c $accent $Tolerance) -or (Test-ColorEq $c $ctl $Tolerance)
            if ($isChip) { if ($run -eq 0) { $start = $x }; $run++ }
            else {
                if ($run -ge 30) { [void]$runs.Add(@{ x = $start; w = $run }) }
                $run = 0
            }
        }
        if ($run -ge 30) { [void]$runs.Add(@{ x = $start; w = $run }) }
    }
    $wantW = @(38, 52, 80)
    $foundAll = $true
    $missing = @()
    foreach ($wv in $wantW) {
        $hit = $false
        foreach ($r in $runs) { if ([Math]::Abs($r.w - $wv) -le 2) { $hit = $true } }
        if (-not $hit) { $foundAll = $false; $missing += $wv }
    }
    $runText = ($runs | ForEach-Object { ("x=" + $_.x + "/w=" + $_.w) }) -join ' '
    if ($runs.Count -eq 0) {
        Add-Row 'geometry' 'ChipWidths' 'INCONCLUSIVE' ('no chip-colour run >=30 px on y=' + $ChipScanY) '38 / 52 / 80 (+-2)' 'chips not present on that scanline'
    }
    else {
        $vd = 'FAIL'; if ($foundAll) { $vd = 'PASS' }
        Add-Row 'geometry' 'ChipWidths' $vd ($runText + ' | missing=' + ($missing -join ',')) '38 / 52 / 80 (+-2)' 'runs of ControlBg #2F2F2F or Accent #9D82C2 on y=' + $ChipScanY
    }
}

# poster / card rects: only judge when the caller supplies a real rect (otherwise UIA required)
if ($PosterRect -eq '') {
    Add-Row 'geometry' 'PosterSize' 'INCONCLUSIVE' 'no -PosterRect supplied' '166x249' 'needs UIA (posters are images, not flat colour runs); pass -PosterRect x,y,w,h to judge'
}
else {
    $pr = $PosterRect.Split(',')
    $pw = [int]$pr[2]; $ph = [int]$pr[3]
    $ok = (($pw -eq 166) -and ($ph -eq 249))
    $vd = 'FAIL'; if ($ok) { $vd = 'PASS' }
    Add-Row 'geometry' 'PosterSize' $vd ("{0}x{1}" -f $pw, $ph) '166x249' 'rect supplied by caller'
}
if ($CardRects -eq '') {
    Add-Row 'geometry' 'CardWidths' 'INCONCLUSIVE' 'no -CardRects supplied' '144 / 216' 'needs UIA (card bounds are not a flat colour run); pass -CardRects x,y,w,h;... to judge'
}
else {
    $vals = @()
    foreach ($one in $CardRects.Split(';')) {
        if ($one.Trim() -eq '') { continue }
        $cc = $one.Split(',')
        $vals += [int]$cc[2]
    }
    $bad = @()
    foreach ($v in $vals) { if (-not (($v -eq 144) -or ($v -eq 216))) { $bad += $v } }
    $vd = 'FAIL'; if ($bad.Count -eq 0) { $vd = 'PASS' }
    Add-Row 'geometry' 'CardWidths' $vd (($vals -join '/') + ' | unexpected=' + ($bad -join ',')) '144 / 216' 'widths supplied by caller'
}
Add-Row 'geometry' 'CornerRadius' 'INCONCLUSIVE' 'not measurable from pixels' 'controls ~8 px / posters ~12 px' 'needs UIA or designer measurement; pixel run-length cannot separate radius from antialiasing'

# ================= 5) WCAG contrast audit (zero dependency, pure math) =================
function Get-RelLum($c) {
    # NOTE: in PowerShell the comma operator binds TIGHTER than '/', so each element needs its own
    # parentheses: @($c.R / 255.0, $c.G / 255.0) would divide by an array (Object[]) and throw.
    $vals = @(($c.R / 255.0), ($c.G / 255.0), ($c.B / 255.0))
    $lin = @()
    foreach ($v in $vals) {
        if ($v -le 0.03928) { $lin += ($v / 12.92) } else { $lin += [Math]::Pow((($v + 0.055) / 1.055), 2.4) }
    }
    return (0.2126 * $lin[0] + 0.7152 * $lin[1] + 0.0722 * $lin[2])
}
function Get-Contrast($fgHex, $bgHex) {
    $l1 = Get-RelLum (Parse-Hex $fgHex)
    $l2 = Get-RelLum (Parse-Hex $bgHex)
    if ($l1 -lt $l2) { $t = $l1; $l1 = $l2; $l2 = $t }
    return [Math]::Round((($l1 + 0.05) / ($l2 + 0.05)), 2)
}

if (-not $SkipContrastStatic) {
    # token pairs from UI_SPEC_SHELL.md section 1; thresholds: 4.5 = normal body text, 3.0 = large text / UI component
    $pairs = @(
        @{ n = 'TextPrimary on RailBg';    fg = '#FFFFFF'; bg = '#202020'; th = 4.5 },
        @{ n = 'TextPrimary on PageBg';    fg = '#FFFFFF'; bg = '#222222'; th = 4.5 },
        @{ n = 'TextPrimary on SurfaceBg'; fg = '#FFFFFF'; bg = '#282828'; th = 4.5 },
        @{ n = 'TextSecondaryHI on RailBg'; fg = '#B5B5B5'; bg = '#202020'; th = 4.5 },
        @{ n = 'TextSecondaryLO on RailBg'; fg = '#9E9E9E'; bg = '#202020'; th = 4.5 },
        @{ n = 'TextSecondaryLO on SurfaceBg'; fg = '#9E9E9E'; bg = '#282828'; th = 4.5 },
        @{ n = 'TextSecondaryLO on RowSelectedBg'; fg = '#9E9E9E'; bg = '#2D2D2D'; th = 4.5 },
        @{ n = 'DisabledText on RailBg';   fg = '#6A6A6A'; bg = '#202020'; th = 3.0 },
        @{ n = 'ServerFail on RailBg';     fg = '#FFADB6'; bg = '#202020'; th = 4.5 },
        @{ n = 'ServerOk on RailBg';       fg = '#50B649'; bg = '#202020'; th = 3.0 },
        @{ n = 'TextDark on Accent';       fg = '#202020'; bg = '#9D82C2'; th = 4.5 }
    )
    foreach ($pr in $pairs) {
        $ratio = Get-Contrast $pr.fg $pr.bg
        $vd = 'FAIL'; if ($ratio -ge $pr.th) { $vd = 'PASS' }
        Add-Row 'contrast' $pr.n $vd ($ratio.ToString() + ' (' + $pr.fg + ' on ' + $pr.bg + ')') ('>= ' + $pr.th) "WCAG ratio = (L1+0.05)/(L2+0.05); threshold 4.5 normal text / 3.0 large or UI component"
    }
    # design rule check: accent #9D82C2 must use DARK text -> white on accent must stay below 4.5
    $whiteOnAccent = Get-Contrast '#FFFFFF' '#9D82C2'
    $darkOnAccent = Get-Contrast '#202020' '#9D82C2'
    $ruleVd = 'FAIL'; if ($whiteOnAccent -lt 4.5) { $ruleVd = 'PASS' }
    Add-Row 'contrast' 'AccentWhiteTextForbidden' $ruleVd ("white=" + $whiteOnAccent + " dark=" + $darkOnAccent) 'white < 4.5 and dark >= 4.5' 'RULE (UI_SPEC section 1: selected chip = accent fill + dark text): if white ever reaches 4.5 this rule is wrong'
}

if ($ContrastProbes -ne '') {
    # grammar: label:#FG@x,y:#BG@x,y:threshold;...
    foreach ($raw in $ContrastProbes.Split(';')) {
        $one = $raw.Trim()
        if ($one -eq '') { continue }
        $bits = $one.Split(':')
        if ($bits.Length -lt 4) { Add-Row 'contrast' $one 'INCONCLUSIVE' 'unparseable contrast probe' 'label:#FG@x,y:#BG@x,y:threshold' 'grammar error'; continue }
        $label = $bits[0]
        $fgHex = $bits[1].Split('@')[0]
        $fgXY = $bits[1].Split('@')[1]
        $bgHex = $bits[2].Split('@')[0]
        $bgXY = $bits[2].Split('@')[1]
        $th = [double]$bits[3]
        $fx = [int]($fgXY.Split(',')[0]) + $originPair.A
        $fy = [int]($fgXY.Split(',')[1]) + $originPair.B
        $bx = [int]($bgXY.Split(',')[0]) + $originPair.A
        $by = [int]($bgXY.Split(',')[1]) + $originPair.B
        if ($fx -lt 0 -or $fy -lt 0 -or $fx -ge $W -or $fy -ge $H -or $bx -lt 0 -or $by -lt 0 -or $bx -ge $W -or $by -ge $H) {
            Add-Row 'contrast' $label 'FAIL' ('out of bounds fg=(' + $fx + ',' + $fy + ') bg=(' + $bx + ',' + $by + ')') ('>= ' + $th) 'probe outside the image'
            continue
        }
        $fgMeas = Get-HexAt $fx $fy
        $bgMeas = Get-HexAt $bx $by
        $ratio = Get-Contrast $fgMeas $bgMeas
        $vd = 'FAIL'; if ($ratio -ge $th) { $vd = 'PASS' }
        Add-Row 'contrast' $label $vd ($ratio.ToString() + ' (fg ' + $fgMeas + ' on bg ' + $bgMeas + ')') ('>= ' + $th) ('pixel-sampled; expected pair ' + $fgHex + ' on ' + $bgHex)
    }
}

# ================= 6) XAML style (passive, opt-in; needs the global tool) =================
if ($XamlPath -ne '') {
    $xstyler = $XamlStylerExe
    if ($xstyler -eq '') {
        $cmd = Get-Command xstyler -ErrorAction SilentlyContinue
        if ($cmd -ne $null) { $xstyler = $cmd.Source }
    }
    if ($xstyler -eq '' -or (-not (Test-Path -LiteralPath $xstyler))) {
        Add-Row 'xamlstyle' 'XamlStylerTool' 'INCONCLUSIVE' 'xstyler not found' 'dotnet tool install --global XamlStyler.Console' 'optional module: install the tool or pass -XamlStylerExe; not a defect of the shell'
    }
    elseif (-not (Test-Path -LiteralPath $XamlPath)) {
        Add-Row 'xamlstyle' 'XamlPath' 'INCONCLUSIVE' ('path not found: ' + $XamlPath) 'directory containing .xaml' 'usage error inside this optional module'
    }
    else {
        # NAME TRAP: -Include with a non-wildcard -Path silently returns EVERY file (see README section 7).
        # So: enumerate everything, then filter by extension explicitly, and drop bin/obj.
        $xfiles = @(Get-ChildItem -Recurse -File -Path $XamlPath -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -eq '.xaml' -and $_.FullName -notmatch '\\(bin|obj)\\' })
        if ($xfiles.Count -eq 0) {
            Add-Row 'xamlstyle' 'XamlFiles' 'INCONCLUSIVE' 'no .xaml outside bin/obj' 'at least one .xaml' 'bin/obj are excluded on purpose (xstyler scans obj when given -d -r)'
        }
        else {
            $csv = ($xfiles | ForEach-Object { $_.FullName }) -join ','
            $xout = & $xstyler -f $csv -p 2>&1
            $xcode = $LASTEXITCODE
            $verdictByFile = @{}
            $curFile = ''
            foreach ($line in $xout) {
                $s = ([string]$line).Trim()
                if ($s.StartsWith('Checking:')) { $curFile = $s.Substring('Checking:'.Length).Trim() }
                elseif ($s -eq 'PASS' -or $s -eq 'FAIL') { if ($curFile -ne '') { $verdictByFile[$curFile] = $s } }
            }
            $mustPass = @()
            foreach ($m in $XamlMustPass.Split(',')) { if ($m.Trim() -ne '') { $mustPass += $m.Trim() } }
            $passFiles = @(); $stockFail = @()
            foreach ($f in $xfiles) {
                $full = $f.FullName
                $v = $verdictByFile[$full]
                if ($v -eq $null) { $v = 'UNKNOWN' }
                $isMust = $false
                foreach ($m in $mustPass) { if ($f.Name -like $m) { $isMust = $true } }
                if ($v -eq 'PASS') {
                    $passFiles += $f.Name
                    Add-Row 'xamlstyle' $f.Name 'PASS' 'xstyler -p: PASS' 'PASS' 'passive check only; no file was modified'
                }
                elseif ($isMust) {
                    Add-Row 'xamlstyle' $f.Name 'FAIL' ('xstyler -p: ' + $v) 'PASS (listed in -XamlMustPass)' 'new or heavily edited file must be format-clean'
                }
                else {
                    $stockFail += $f.Name
                    Add-Row 'xamlstyle' $f.Name 'INCONCLUSIVE' ('xstyler -p: ' + $v) 'PASS for new files only' 'existing file: allowed to FAIL; queued for one-shot formatting by t34 (single commit, easy to review)'
                }
            }
            Add-Row 'xamlstyle' 'XamlStylerSummary' 'PASS' ('pass=' + $passFiles.Count + ' queued=' + $stockFail.Count + ' total=' + $xfiles.Count + ' exit=' + $xcode) 'pass counts + queue list' ('queue: ' + (($stockFail | Sort-Object) -join ' '))
        }
    }
}

# ================= output =================
foreach ($r in $rowsOut) {
    Write-Output ("RESULT|group=" + $r.group + "|item=" + $r.item + "|verdict=" + $r.verdict + "|measured=" + $r.measured + "|expected=" + $r.expected + "|note=" + $r.note)
}
$passN = 0; $failN = 0; $incN = 0
foreach ($r in $rowsOut) {
    if ($r.verdict -eq 'PASS') { $passN++ } elseif ($r.verdict -eq 'FAIL') { $failN++ } else { $incN++ }
}
$overall = 'PASS'
$exitCode = 0
if ($failN -gt 0) { $overall = 'FAIL'; $exitCode = 1 }
elseif ($incN -gt 0) { $overall = 'INCONCLUSIVE'; $exitCode = 0 }
Write-Output ("SUMMARY|verdict=" + $overall + "|pass=" + $passN + "|fail=" + $failN + "|inconclusive=" + $incN + "|exit=" + $exitCode)

if ($Json -ne '') {
    $obj = @{
        file = $Image; bytes = $fi.Length; sha256_12 = $sha
        mtime = $fi.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
        size = ($W.ToString() + 'x' + $H); channel = $Channel; windowkind = $WindowKind
        origin = $originText; sampled = $sampleTime
        nonBlackPct = $nonBlackPct3; contentRows = $contentRows
        verdict = $overall; pass = $passN; fail = $failN; inconclusive = $incN
        results = $rowsOut
    }
    $jsonText = ($obj | ConvertTo-Json -Depth 6)
    [System.IO.File]::WriteAllText($Json, $jsonText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("JSON|path=" + $Json)
}

exit $exitCode
