# Grid sampler for evidence screenshots (t26 / ui). ASCII-only on purpose.
# Reads an existing PNG and prints the pixel colour at a grid of (x,y) points,
# so the layout and the client-area offset can be read off real numbers instead of eyeballing.
param(
  [Parameter(Mandatory=$true)][string]$Png,
  [string]$Rows = '0,4,10,24,40,60,100,150,200,250,300,400,500,560,700,750',
  [string]$Cols = '0,2,4,10,60,120,180,240,250,254,255,256,257,260,300,400,600,900,1200,1435'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$bmp = [System.Drawing.Bitmap]::FromFile($Png)
Write-Host ("IMG {0} {1}x{2} format={3}" -f $Png, $bmp.Width, $bmp.Height, $bmp.PixelFormat)

foreach ($ry in $Rows.Split(',')) {
  $y = [int]$ry
  if ($y -ge $bmp.Height) { continue }
  $sb = New-Object System.Text.StringBuilder
  foreach ($cx in $Cols.Split(',')) {
    $x = [int]$cx
    if ($x -ge $bmp.Width) { continue }
    $c = $bmp.GetPixel($x, $y)
    [void]$sb.Append(("{0},#{1:X2}{2:X2}{3:X2}  " -f $x, $c.R, $c.G, $c.B))
  }
  Write-Host ("ROW y={0}: {1}" -f $y, $sb.ToString())
}

$bmp.Dispose()
exit 0
