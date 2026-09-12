<#
  doc-gate.ps1 -- documentary hygiene gate (turn tonight's repeatedly-violated rules into executable checks)

  WHY A SCRIPT INSTEAD OF A RULE:
    The same failure family hit us 6+ times tonight (control-byte contamination, synthetic token
    residue, PowerShell's three liars (location / encoding / string expansion), BOM silently
    dropped by the edit tool, "15/15" written without a sample time...). Even the people who
    WROTE the rules violated them. => "remember the rule" is not dependable; make it a scanner.

  WHY THIS FILE IS PURE ASCII:
    Windows PowerShell 5.1 decodes BOM-less UTF-8 as ANSI/GBK. A previous revision of this very
    script contained Chinese text and CRASHED with a parse error for exactly that reason.
    Keeping it ASCII-only makes the file immune to that trap regardless of who edits it
    (any later `edit` on this file would silently drop a BOM anyway).

  FOUR CHECKS (each one can fail):
    1) control bytes: bytes < 0x20 other than 0x0A/0x0D inside text, expect 0.
       Exceptions must be scoped by "path + shape", never by a CATEGORY claim such as
       "these files are UTF-16" -- otherwise a real corruption gets silently filtered out
       by our own exception (WORKSPACE fact 98).
    2) credential residue: files matching api_key=[0-9a-fA-F]{8}, expect 0.
    3) BOM: reported per file. NO pass/fail assertion -- some files deliberately carry a BOM
       (e.g. BASELINE_SHELL.md); asserting would turn a design choice into a defect.
    4) frozen area: `git status --porcelain -- reversed/` must be empty.

  COUNTER-CONTROL (proves the scanner is not a no-op):
    Build a temp file containing 0x07, assert the scanner reports 1, then delete it.

  USAGE (from repo root):
      powershell -NoProfile -ExecutionPolicy Bypass -File shell\tools\doc-gate.ps1
  EXIT: 0 = all pass; 1 = some check failed; 2 = counter-control failed (conclusions unreliable)
#>
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ScanRoot = 'shell',
    [string]$NoSuchRootProbe = 'shell/__no_such_dir__'
)

$ErrorActionPreference = 'Stop'
$script:fail = 0
$script:counterControlOk = $false

function Say([string]$text) { Write-Host $text }

function Get-CtrlBytes([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $n = 0
    foreach ($b in $bytes) { if ($b -lt 0x20 -and $b -ne 0x0A -and $b -ne 0x0D) { $n++ } }
    return $n
}

function Get-BomState([string]$path) {
    $fs = [System.IO.File]::OpenRead($path)
    try {
        $b = New-Object byte[] 3
        $read = $fs.Read($b, 0, 3)
        if ($read -eq 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { return 'True' }
        return 'False'
    } finally { $fs.Dispose() }
}

function Get-DocFiles([string]$root) {
    $p = Join-Path $RepoRoot $root
    if (-not (Test-Path $p)) { return @() }
    return @(Get-ChildItem -Path $p -Recurse -Filter *.md -File -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Sort-Object FullName)
}

Say "=== doc-gate ==="
Say ("repo        = " + $RepoRoot)
Say ("scan-root   = " + $ScanRoot + "   (whole tree; bin/obj excluded)")
Say ("sample-time = " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

# ---------- 0a) counter-control: the control-byte scanner must detect known contamination ----------
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("docgate_ctl_" + [Guid]::NewGuid().ToString('N') + ".md")
[System.IO.File]::WriteAllBytes($tmp, [byte[]](0x41, 0x0A, 0x07, 0x42, 0x0A))
$ctl = Get-CtrlBytes $tmp
Remove-Item -Force $tmp
if ($ctl -eq 1) {
    $script:counterControlOk = $true
    Say "[COUNTER-CONTROL] OK    41 0A 07 42 0A => ctrl=1  (scanner is not a no-op)"
} else {
    Say ("[COUNTER-CONTROL] FAIL  expected ctrl=1, got " + $ctl + "  => all conclusions below are UNRELIABLE")
}

# ---------- 0b) counter-control: the empty-glob guard must be meaningful ----------
# A file enumeration that matches 0 items does NOT error (DESIGN 9.6 rule A); a gate that
# silently reports PASS on an empty scan is worse than no gate. Prove the guard can fire.
$probe = Get-DocFiles $NoSuchRootProbe
if ($probe.Count -eq 0) {
    Say ("[COUNTER-CONTROL] OK    nonexistent root '" + $NoSuchRootProbe + "' => 0 files (empty-scan guard is meaningful)")
} else {
    Say "[COUNTER-CONTROL] FAIL  nonexistent root returned files => guard cannot fire"
    $script:fail++
}

# ---------- 1) control bytes ----------
Say ""
Say "--- 1) control bytes (expect 0) ---"
$docs = Get-DocFiles $ScanRoot
Say ("scan-root = " + $ScanRoot + "   files = " + $docs.Count + "   (count measured at sample time; never hardcode it)")
if ($docs.Count -eq 0) {
    Say ("GLOB-EMPTY FAIL  '" + $ScanRoot + "' matched 0 files  => refusing to report PASS")
    $script:fail++
}
$ctrlDirty = @()
foreach ($f in $docs) {
    $n = Get-CtrlBytes $f.FullName
    if ($n -ne 0) { $ctrlDirty += ($f.Name + " ctrl=" + $n) }
}
if ($ctrlDirty.Count -eq 0) { Say "CTRL=0  PASS" }
else { Say ("CTRL FAIL  " + ($ctrlDirty -join ' | ')); $script:fail++ }

# ---------- 2) credential residue ----------
Say ""
Say "--- 2) credential residue: api_key=[0-9a-fA-F]{8} (expect 0 files) ---"
Push-Location $RepoRoot
try {
    $hits = @(& git grep -lE 'api_key=[0-9a-fA-F]{8}' -- . 2>$null)
} finally { Pop-Location }
if ($hits.Count -eq 0) { Say "CLEAN  PASS  (0 files)" }
else { Say ("FAIL  hit files: " + ($hits -join ', ')); $script:fail++ }

# ---------- 3) BOM (report only) ----------
Say ""
Say "--- 3) BOM (report only, no assertion) ---"
foreach ($f in $docs) { Say ("  " + $f.Name.PadRight(36) + " BOM=" + (Get-BomState $f.FullName)) }

# ---------- 4) frozen area ----------
Say ""
Say "--- 4) frozen area: reversed/ (expect clean) ---"
Push-Location $RepoRoot
try {
    $st = @(& git status --porcelain -- reversed/ 2>$null)
} finally { Pop-Location }
if ($st.Count -eq 0) { Say "CLEAN  PASS  (git status -- reversed/ produced no output)" }
else { Say ("FAIL  reversed/ modified: " + ($st -join ' | ')); $script:fail++ }

Say ""
if (-not $script:counterControlOk) { Say "RESULT=UNRELIABLE (counter-control failed)"; exit 2 }
if ($script:fail -eq 0) { Say "RESULT=PASS"; exit 0 }
Say ("RESULT=FAIL (" + $script:fail + " check(s))"); exit 1
