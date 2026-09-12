# Run-length scanline dump for evidence screenshots (t26 / ui). ASCII-only on purpose.
# Prints every colour transition along one row (y) or one column (x), so a boundary
# (e.g. the 256 px sidebar edge) can be read off as an exact pixel index.
param(
  [Parameter(Mandatory=$true)][string]$Png,
  [int]$RowY = -1,
  [int]$ColX = -1,
  [int]$From = 0,
  [int]$To = -1,
  [int]$MinRun = 2
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$bmp = [System.Drawing.Bitmap]::FromFile($Png)
Write-Host ("IMG {0} {1}x{2}" -f $Png, $bmp.Width, $bmp.Height)

function Hex($c) { return ("#{0:X2}{1:X2}{2:X2}" -f $c.R, $c.G, $c.B) }

if ($RowY -ge 0) {
  if ($To -lt 0) { $To = $bmp.Width - 1 }
  $prev = ''
  $runStart = $From
  $out = New-Object System.Text.StringBuilder
  for ($x = $From; $x -le $To; $x++) {
    $h = Hex $bmp.GetPixel($x, $RowY)
    if ($h -ne $prev) {
      if ($prev -ne '' -and ($x - $runStart) -ge $MinRun) {
        [void]$out.Append(("{0}..{1}={2}  " -f $runStart, ($x - 1), $prev))
      }
      $prev = $h
      $runStart = $x
    }
  }
  [void]$out.Append(("{0}..{1}={2}" -f $runStart, $To, $prev))
  Write-Host ("SCANROW y={0} from={1} to={2}" -f $RowY, $From, $To)
  Write-Host $out.ToString()
}

if ($ColX -ge 0) {
  if ($To -lt 0) { $To = $bmp.Height - 1 }
  $prev = ''
  $runStart = $From
  $out = New-Object System.Text.StringBuilder
  for ($y = $From; $y -le $To; $y++) {
    $h = Hex $bmp.GetPixel($ColX, $y)
    if ($h -ne $prev) {
      if ($prev -ne '' -and ($y - $runStart) -ge $MinRun) {
        [void]$out.Append(("{0}..{1}={2}  " -f $runStart, ($y - 1), $prev))
      }
      $prev = $h
      $runStart = $y
    }
  }
  [void]$out.Append(("{0}..{1}={2}" -f $runStart, $To, $prev))
  Write-Host ("SCANCOL x={0} from={1} to={2}" -f $ColX, $From, $To)
  Write-Host $out.ToString()
}

$bmp.Dispose()
exit 0
