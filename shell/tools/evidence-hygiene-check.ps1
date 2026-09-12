# evidence-hygiene-check.ps1 -- two gates that guard evidence quality (t48):
#
#   H1 (privacy / capture surface):
#     No capture script may contain a screen-copy API. The check scans .ps1/.cs under the given roots for the
#     CASE-SENSITIVE tokens CopyFromScreen / BitBlt on NON-COMMENT lines only. popup-capture*.ps1 is the declared
#     safe path (PrintWindow on an explicit HWND, no screen copy) and is allow-listed by default. Anything else
#     that hits => FAIL with  file:line  and the offending line, because a screen copy of a window that is not
#     forced to the foreground grabs the whole user desktop (this already happened twice in this project).
#     -ExtraScanPath adds extra roots so the checker itself can be counter-controlled with a synthetic sample.
#
#   H2 (citation hygiene):
#     Every file that a COMMITTED .md cites as evidence/<name> must itself be in version control. A cited file
#     that git does not track (e.g. swallowed by .gitignore's "*.log") => FAIL, with the citing document:line.
#     Files that are cited but do not exist on disk are reported as FAIL too when the cited name looks complete
#     (has an extension); wrapped/truncated fragments are reported as NOTES instead of failures.
#
#   H6 (irreversible action / batch kill by name), authorized by the captain 2026-09-12 (fact 221):
#     No .ps1/.cs under the scan roots may END processes it did not start. Ending processes BY NAME
#     (Stop-Process -Name | taskkill /IM | GetProcessesByName(...) followed within 5 lines by .Kill(/
#     Stop-Process) is always a batch action over OTHER people's instances (teammate window, user session,
#     another agent's run in flight) and it is irreversible. The compliant shape is: keep the object that your
#     own Start-Process/Process.Start returned and stop only that pid; if a stale instance must go, PRINT a
#     STALE-PRESENT report (path + pid + start time) and exit instead of killing. A token that occurs only
#     inside a quoted literal is SEARCH-ONLY and is reported as a NOTE, not a hit (this tool itself searches
#     for those strings). Ratchet: the stock measured at introduction is the baseline and may only decrease.
#
# Exit codes: 0 = no FAIL, 1 = at least one FAIL, 2 = usage error.
# Output: machine-readable lines  CHECK| / FINDING| / STOCK| / NOTE| / SUMMARY|  (ASCII-only script, PS 5.1 safe).
#         STOCK| = a measured existing stock that is NOT a defect (formerly emitted as FINDING|H7|EMOJI|);
#         the H7 ratchet verdict lives ONLY in CHECK|H7-emoji-in-ui-copy|verdict=  (captain ruling 2026-09-12).
#
# Invoke (this host blocks .ps1 by policy):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1 -Json %TEMP%\hygiene.json

[CmdletBinding()]
param(
    [string]$ScanRoot = 'shell',
    [string]$ExtraScanPath = '',
    [string]$DocRoot = 'shell',
    [string]$EvidenceDir = 'shell/Tests/evidence',
    [string]$ExtraEvidencePath = '',
    # H3b ratchet -- MEASURED OBJECT = PRODUCTION COUNTS (captain 2026-09-12 ruling B), baselines = 0/0.
    # Captain 2026-09-12 approved the defaults 0/0 explicitly. Names NOTE: the two OLD parameters
    # (-H3bBareBaseline / -H3bRibBaseline) were REPLACED by these four per ruling B (each group gets its own
    # quota); the old TOTAL pair is still PRINTED as totalBaselineBare / totalBaselineRib ( = group baselines
    # summed), so the "total 0/0" the captain asked for is visible without resurrecting a parameter that no
    # longer gates anything. THE TEACHING FACT: with the old 22/5 defaults a real new bare catch printed
    # PASS (1 <= 22) -- "after the stock reaches zero the baseline MUST follow it down to zero" is not a
    # slogan, it is a measured conclusion.
    # KEEP THIS BLOCK ASCII-ONLY. A CJK comment here once swallowed the following newline when PS 5.1
    # decoded this BOM-less script as ANSI: the next line ([int]$H3bProdBareBaseline = 0,) was eaten by the
    # comment, the parameter became undefined and the CHECK line printed an EMPTY prodBaseline.
    # That is README 10.8-3's mechanism reproduced inside this very tool -- do not reintroduce it.
    [int]$H3bProdBareBaseline = 0,      # production bare catches (= owner mapped): any increase => FAIL
    [int]$H3bProdRibBaseline = 0,       # production reason-inside-braces
    [int]$H3bUnmappedBareCap = 0,       # in-repo probe/fixture hits: their own quota, must not grow either
    [int]$H3bUnmappedRibCap = 0,
    [string]$H3bBaselineAt = '615e8b9',
    # H6 -- captain 2026-09-12 fact 221 authorized this gate ("the gate must cover IRREVERSIBLE ACTIONS --
    # killing processes, deleting files, writing credentials -- not style"). His ruling named baseline=1,
    # because that was the stock BEFORE ui fixed shell/App/tools/t26-evidence.ps1; the stock is MEASURED here
    # and printed with baselineAt instead of being assumed. Ratchet rule unchanged: the baseline may only
    # decrease, and a NEW hit is what fails.
    # LOWERED 1 -> 0 on 2026-09-12 by verifier under the captain's explicit approval (his 2026-09-12 message,
    # "approved lowering the baseline;" the literal wording stays in the ledger, not in this ASCII-only file),
    # after the measured stock reached realHits=0 / byOwner=none (ui2 removed the KernelLogMasker.cs hit).
    # History: stock was 1 when this gate was introduced (baselineAt ad40a00); baselineAt now names the commit
    # of the lowering. Only the captain or verifier may lower this; nobody else touches it.
    [int]$H6BatchKillBaseline = 0,
    [string]$H6BaselineAt = '8d4d117',
    # H7 emoji/U+FE0F (captain 2026-09-12; scanner body contributed by ui2). Baselines are MEASURED stock:
    # the ratchet may only decrease, and a caller can LOWER a baseline to force a FAIL (reverse control).
    [string]$EmojiScope = 'shell/App',
    [int]$EmojiAstralBaseline = 44,     # t301: LOWERED 53 -> 44 = measured committed stock (stockSource=committed(HEAD))
    [int]$EmojiFe0fBaseline = 49,      # t301: LOWERED 51 -> 49 = measured committed stock. RATCHET REASON: baseline 51 left a silent margin of 2 fe0f / 9 astral, so two NEW U+FE0F still printed PASS.
    [string]$EmojiBaselineAt = 'c99c45a',   # t301: commit at which 44/49 were MEASURED (was ba2acad for 53/51)
    [string]$OwnerMapFile = 'shell/tools/hygiene-owner-map.txt',
    [string]$AllowListFile = 'shell/tools/privacy-allowlist.txt',
    [string]$Json = ''
)

$ErrorActionPreference = 'Continue'

# GATE-START (captain 2026-09-12): the anchor line that makes "this gate actually RAN" decidable.
# Why: `& .\shell\tools\evidence-hygiene-check.ps1` can fail BEFORE the first statement (ExecutionPolicy on
# this host blocks .ps1; there is no `pwsh` on this machine), and the caller then sees an exit code that is
# NOT a verdict -- i.e. a FALSE GREEN. RULE: no GATE-START or no SUMMARY => the gate did not run, and the exit
# code must not be read as a verdict. Same family as "the control itself can lie".
$gateScript = Split-Path -Leaf $MyInvocation.MyCommand.Path
$gateHead = ''
try { $gateHead = ((& git rev-parse --short HEAD 2>$null) | Select-Object -First 1) } catch { $gateHead = '<git failed>' }
# RUN IDENTITY (t258): the gate writes a TEMP file (H7 HEAD-side blob copy) and two gate runs can overlap --
# two teammates running the gate in the same window is normal here. A FIXED temp name made the two runs share
# one path: run B's blob overwrote run A's between A's `cmd /c git cat-file` and A's ReadAllText, so A counted
# a DIFFERENT revision's file (or a half-written one). Nothing in the output said which process produced which
# line, so the collision was unprovable after the fact. pid + runId (pid + process start milliseconds) make each
# run self-identifying; the temp path carries runId so two runs cannot share a file at all.
$gatePid = $PID
$gateStartMs = 0
try {
    $gateSelfProc = Get-Process -Id $PID -ErrorAction Stop
    $gateStartMs = [long](([DateTimeOffset]$gateSelfProc.StartTime).ToUnixTimeMilliseconds())
} catch { $gateStartMs = 0 }
if ($gateStartMs -eq 0) { $gateStartMs = [long](([DateTimeOffset](Get-Date)).ToUnixTimeMilliseconds()) }
$gateRunId = "$gatePid-$gateStartMs"
Write-Output ("GATE-START|script=" + $gateScript + "|ps=" + $PSVersionTable.PSVersion.ToString() + "|time=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|head=" + $gateHead + "|cwd=" + (Get-Location).Path + "|pid=" + $gatePid + "|runId=" + $gateRunId)

$results = New-Object System.Collections.ArrayList
$notes = New-Object System.Collections.ArrayList

function Add-Check([string]$id, [string]$verdict, [string]$measured, [string]$expected, [string]$note) {
    [void]$results.Add(@{ id = $id; verdict = $verdict; measured = $measured; expected = $expected; note = $note })
}
function Add-Note([string]$text) { [void]$notes.Add($text) }

$repoRoot = (Get-Location).Path

# ---------------------------------------------------------------- helpers
function Test-CommentLine([string]$line) {
    $t = $line.Trim()
    if ($t -eq '') { return $true }
    if ($t.StartsWith('#')) { return $true }
    if ($t.StartsWith('//')) { return $true }
    if ($t.StartsWith('*')) { return $true }
    if ($t.StartsWith('/*')) { return $true }
    return $false
}
function Get-RelPath([string]$full) {
    $r = $repoRoot.TrimEnd('\') + '\'
    if ($full.StartsWith($r, [System.StringComparison]::OrdinalIgnoreCase)) { return $full.Substring($r.Length).Replace('\', '/') }
    return $full.Replace('\', '/')
}
function Test-GitTracked([string]$relPath) {
    $out = & git ls-files --error-unmatch -- $relPath 2>&1 | Out-String
    $code = $LASTEXITCODE
    return @{ Tracked = ($code -eq 0); Raw = $out.Trim() }
}
function Test-GitIgnored([string]$relPath) {
    $out = & git check-ignore -- $relPath 2>&1 | Out-String
    $code = $LASTEXITCODE
    return ($code -eq 0)
}

# ---------------------------------------------------------------- allow-list
# captain 2026-09-12 (#4, from ui3): the H1 grading must be a MACHINE-CHECKABLE FIELD, not something the
# reader has to guess from the prose. Every entry that authorizes a REAL capture call must carry:
#     rect-source=<foreground-window|own-window>   owner=<name>
# The two values are DIFFERENT premises and must not be merged:
#   own-window        = the rect comes from the script's own HWND (never needs foregrounding)
#   foreground-window = the script must bring the window to the foreground FIRST, otherwise CopyFromScreen
#                       grabs whatever happens to be on top (this is the D3D fallback: PrintWindow is black)
# A file whose occurrences of the token sit INSIDE quoted literals is classified SEARCH-ONLY mechanically
# (this tool searches for the token; it never captures) and needs no entry at all.
$allowEntries = New-Object System.Collections.ArrayList
[void]$allowEntries.Add(@{ Pat = '^shell/tools/popup-capture.*\.ps1$'; Rect = 'own-window'; Owner = 'builtin'; Raw = 'built-in safe module (PrintWindow + explicit HWND)' })
if (Test-Path -LiteralPath $AllowListFile) {
    foreach ($line in [System.IO.File]::ReadAllLines($AllowListFile)) {
        $t = $line.Trim()
        if ($t -eq '' -or $t.StartsWith('#')) { continue }
        $pat = $t
        $at = $t.IndexOf(' # ')
        if ($at -gt 0) {
            $pat = $t.Substring(0, $at).Trim()
        }
        else {
            # captain 2026-09-12: an entry written as `<regex>    rect-source=... owner=...` (NO ' # ')
            # used to have the FIELD TEXT merged into the regex, so it never matched: "looks registered,
            # is not registered". Strip the trailing fields instead -- and SAY SO, because a silent
            # fallback is how the next author gets surprised. (` # ` stays the preferred, explicit form.)
            $cut = -1
            foreach ($tok in @('rect-source=', 'owner=')) {
                $p = $t.IndexOf($tok)
                if ($p -gt 0 -and ($cut -lt 0 -or $p -lt $cut)) { $cut = $p }
            }
            if ($cut -gt 0) {
                $pat = $t.Substring(0, $cut).Trim()
                $pat = ($pat -replace '\s+#\s*$', '')   # tolerate `pattern #` with no trailing space
                $pat = $pat.Trim()
                Add-Note ("H1 allow-list entry WITHOUT the ' # ' separator: pattern='" + $pat + "' (fields were stripped off the line end; prefer '<regex> # <reason> rect-source=... owner=...')")
            }
            else {
                $hash = $t.IndexOf('#')
                if ($hash -gt 0) { $pat = $t.Substring(0, $hash).Trim() }
            }
        }
        $rect = ''; $owner = ''
        $mr = [regex]::Match($t, 'rect-source=([A-Za-z][A-Za-z\-]*)'); if ($mr.Success) { $rect = $mr.Groups[1].Value }
        $mo = [regex]::Match($t, 'owner=([A-Za-z0-9_\-\.]+)'); if ($mo.Success) { $owner = $mo.Groups[1].Value }
        [void]$allowEntries.Add(@{ Pat = $pat; Rect = $rect; Owner = $owner; Raw = $t })
        $shownRect = $rect; if ($shownRect -eq '') { $shownRect = '<MISSING>' }
        $shownOwner = $owner; if ($shownOwner -eq '') { $shownOwner = '<MISSING>' }
        Add-Note ("allow-list entry from " + $AllowListFile + ": " + $pat + " | rect-source=" + $shownRect + " | owner=" + $shownOwner)
    }
}
else {
    Add-Note ("no allow-list file at " + $AllowListFile + " (only the built-in popup-capture rule applies)")
}
function Test-QuotedToken([string]$oneLine, [string]$tokPat) {
    # true when the FIRST occurrence of the token on that line sits inside a '...' or "..." literal
    # (i.e. the line merely SEARCHES for or PRINTS the token; a real call is never quoted)
    $m = [regex]::Match($oneLine, $tokPat)
    if (-not $m.Success) { return $false }
    $before = $oneLine.Substring(0, $m.Index)
    $sq = ([regex]::Matches($before, "'")).Count
    $dq = ([regex]::Matches($before, '"')).Count
    return ((($sq % 2) -eq 1) -or (($dq % 2) -eq 1))
}

# ---------------------------------------------------------------- H1 privacy scan
$scanRoots = New-Object System.Collections.ArrayList
[void]$scanRoots.Add($ScanRoot)
if ($ExtraScanPath -ne '') {
    foreach ($p in $ExtraScanPath.Split(';')) { if ($p.Trim() -ne '') { [void]$scanRoots.Add($p.Trim()) } }
}
$privacyHits = New-Object System.Collections.ArrayList
$screenRectHits = New-Object System.Collections.ArrayList
$screenSourceNotes = New-Object System.Collections.ArrayList
$scanned = 0
foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { Add-Note ("scan root not found: " + $root); continue }
    $files = @(Get-ChildItem -Recurse -File -Path $root -ErrorAction SilentlyContinue |
        Where-Object { ($_.Extension -eq '.ps1' -or $_.Extension -eq '.cs') -and $_.FullName -notmatch '\\(bin|obj)\\' })
    foreach ($f in $files) {
        $scanned++
        $rel = Get-RelPath $f.FullName
        $allLines = [System.IO.File]::ReadAllLines($f.FullName)
        $text = ($allLines -join "`n")
        # boundary evidence: the script must show the same three guards popup-capture.ps1 has.
        # NOTE: the guards are looked for on NON-COMMENT lines only -- a guard mentioned in a comment is a
        # promise, not a guard (same "shape != hazard" reasoning as the token itself).
        $textCode = ((@($allLines | Where-Object { -not (Test-CommentLine $_) })) -join "`n")
        $hasDesktopGuard = ($textCode -cmatch 'Progman|WorkerW|Shell_TrayWnd')
        $hasCoverageGuard = ($textCode -cmatch '0\.95|95\s*%|virtualDesktop|screenArea|VIRTUALSCREEN')
        $hasPidGuard = ($textCode -cmatch 'GetWindowThreadProcessId|TargetPid|-Pid\b|ProcessId')
        $hasAssertion = ($textCode -cmatch 'sidecar|ASSERTION|sha256_12|SOURCE ASSERTION')
        $hasBoundary = ($hasDesktopGuard -and $hasCoverageGuard -and $hasPidGuard -and $hasAssertion)
        $i = 0
        foreach ($line in $allLines) {
            $i++
            if (Test-CommentLine $line) { continue }
            if ($line -cmatch 'CopyFromScreen|BitBlt') {
                $allowed = $false; $rect = ''; $owner = ''
                foreach ($e in $allowEntries) { if ($rel -match $e.Pat) { $allowed = $true; $rect = $e.Rect; $owner = $e.Owner } }
                [void]$privacyHits.Add(@{ Path = $rel; Line = $i; Allowed = $allowed; Boundary = $hasBoundary; SearchOnly = (Test-QuotedToken $line 'CopyFromScreen|BitBlt'); Rect = $rect; Owner = $owner; Text = $line.Trim() })
            }
            if ($line -cmatch 'PrimaryScreen|VirtualScreen|SystemInformation\.VirtualScreen|GetSystemMetrics\((0|1|78|79)\)') {
                # H1b only cares when the SCREEN-sized source feeds the capture call itself; computing the virtual
                # desktop size to implement the >=95% guard (as popup-capture.ps1 does) is legitimate.
                if ($line -cmatch 'CopyFromScreen|BitBlt|\$rect\s*=|Rect\s*=\s*') {
                    [void]$screenRectHits.Add(@{ Path = $rel; Line = $i; Text = $line.Trim() })
                }
                else {
                    [void]$screenSourceNotes.Add(@{ Path = $rel; Line = $i; Text = $line.Trim() })
                }
            }
        }
        # SEARCH-ONLY is a property of the WHOLE FILE: one unquoted call anywhere makes every hit in it real
        $fileHits = @($privacyHits | Where-Object { $_.Path -eq $rel })
        $allQuoted = ($fileHits.Count -gt 0) -and (@($fileHits | Where-Object { -not $_.SearchOnly }).Count -eq 0)
        foreach ($fh in $fileHits) { $fh.SearchOnly = $allQuoted }
    }
}
$searchOnlyHits = @($privacyHits | Where-Object { $_.SearchOnly })
$realHits = @($privacyHits | Where-Object { -not $_.SearchOnly })
$entryMatchedHits = @($realHits | Where-Object { $_.Allowed })
$badHits = @($realHits | Where-Object { -not ($_.Allowed -and $_.Boundary -and ($_.Rect -eq 'foreground-window' -or $_.Rect -eq 'own-window') -and $_.Owner -ne '') })
# NOTE ON NAMES (captain 2026-09-12 ruling): the CHECK line must show BOTH quantities side by side, so a
# reader can never again read "fully allowed = 0" as "no entry matched".
#   allowed=              the hit matched a COMPLETE entry (fields included) AND the script shows the four guards
#   registeredNoBoundary= the hit matched a COMPLETE entry but the script does NOT show the four guards
#                         (still violating -- the entry half is done, the script half is not)
#   entriesMatched=       the hit matched a COMPLETE entry, guards not considered (the disambiguator)
# This is a READABILITY change only -- no new gate policy (captain's freeze on new rules stands).
$allowedCount = @($realHits | Where-Object { $_.Allowed -and $_.Boundary -and ($_.Rect -eq 'foreground-window' -or $_.Rect -eq 'own-window') -and $_.Owner -ne '' }).Count
$regNoBoundary = @($realHits | Where-Object { $_.Allowed -and (-not $_.Boundary) }).Count
$h1Measured = ("scanned=" + $scanned + " nonCommentHits=" + $privacyHits.Count + " searchOnly=" + $searchOnlyHits.Count + " real=" + $realHits.Count + " entriesMatched=" + @($realHits | Where-Object { $_.Allowed }).Count + " allowed=" + $allowedCount + " registeredNoBoundary=" + $regNoBoundary + " violating=" + $badHits.Count)
$h1Expected = 'screen copy only from an explicit window rect, with desktop/95%/pid guards and a source assertion AND registered in the allow-list with rect-source=<foreground-window|own-window> + owner=<name>'
if ($badHits.Count -eq 0) {
    Add-Check 'H1-privacy-capture-surface' 'PASS' $h1Measured $h1Expected 'token-level ban replaced by a source-and-boundary criterion (captain 2026-09-12); ALLOWED requires BOTH the four guards and a COMPLETE allow-list entry (reason + rect-source + owner); a token inside a quoted literal is SEARCH-ONLY'
}
else {
    Add-Check 'H1-privacy-capture-surface' 'FAIL' $h1Measured $h1Expected 'a screen copy of a non-foreground window captures the user desktop; the allow-list entry is the owner-written justification (not written by this tool)'
}
foreach ($h in $privacyHits) {
    if ($h.SearchOnly) {
        Write-Output ("NOTE|H1|search-only-literal|" + $h.Path + ":" + $h.Line + "|" + $h.Text + "|advice=the token sits inside a quoted literal: this line searches for or prints it, it does not capture")
        continue
    }
    $rectOk = ($h.Rect -eq 'foreground-window' -or $h.Rect -eq 'own-window')
    $tag = 'VIOLATION'
    if ($h.Allowed -and $h.Boundary -and $rectOk -and $h.Owner -ne '') { $tag = 'ALLOWED' }
    elseif ($h.Allowed -and ((-not $rectOk) -or $h.Owner -eq '')) { $tag = 'VIOLATION-MISSING-FIELD' }
    elseif ($h.Boundary) { $tag = 'BOUNDARY-UNREGISTERED' }
    elseif ($h.Allowed) { $tag = 'REGISTERED-NO-BOUNDARY' }
    $fix = ''
    if ($tag -ne 'ALLOWED') {
        $fix = '|requiredFix=BOTH are required: (a) an entry in the allow-list with a concrete reason AND the two machine-checkable fields rect-source=<foreground-window|own-window> (foreground-window means the script must bring the window to the front FIRST; own-window means the rect comes from the script''s own HWND and needs no foregrounding) AND owner=<name> AND (b) the four guards in the script (desktop-class + >=95%-virtual-desktop + pid + a written hwnd/pid/rect source assertion); otherwise switch to popup-capture.ps1'
        if ($tag -eq 'VIOLATION-MISSING-FIELD') { $fix += '; the entry exists but rect-source/owner is missing or invalid -- grading must be a field, not prose' }
    }
    Write-Output ("FINDING|H1|" + $tag + "|" + $h.Path + ":" + $h.Line + "|" + $h.Text + $fix)
}
# H1b: a screen-level rect source is never acceptable, even with guards
if ($screenRectHits.Count -eq 0) {
    Add-Check 'H1b-no-screen-level-rect' 'PASS' 'screenRectSources=0' 'no PrimaryScreen / VirtualScreen / GetSystemMetrics rect source in capture scripts' 'full-screen capture is never allowed'
}
else {
    Add-Check 'H1b-no-screen-level-rect' 'FAIL' ("screenRectSources=" + $screenRectHits.Count) 'no PrimaryScreen / VirtualScreen / GetSystemMetrics rect source in capture scripts' 'these expressions produce a screen-sized rect regardless of any later guard'
}
foreach ($s in $screenRectHits) {
    Write-Output ("FINDING|H1b|SCREEN-RECT-SOURCE|" + $s.Path + ":" + $s.Line + "|" + $s.Text)
}
foreach ($s in $screenSourceNotes) {
    Write-Output ("NOTE|H1b|screen-size-used-but-not-as-capture-rect|" + $s.Path + ":" + $s.Line + "|" + $s.Text)
}

# ---------------------------------------------------------------- H2 citation hygiene
$cited = @{}
$mdFiles = @(Get-ChildItem -Recurse -File -Path $DocRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq '.md' -and $_.FullName -notmatch '\\(bin|obj)\\' })
foreach ($m in $mdFiles) {
    $relDoc = Get-RelPath $m.FullName
    $text = [System.IO.File]::ReadAllText($m.FullName)
    foreach ($mm in [regex]::Matches($text, 'evidence[/\\]([A-Za-z0-9_][A-Za-z0-9._\-]*(?:[\r\n]\s*[A-Za-z0-9._\-]+)*)')) {
        $name = ($mm.Groups[1].Value -replace '[\r\n]', '').Trim()
        if ($name -eq '') { continue }
        if (-not $cited.ContainsKey($name)) { $cited[$name] = New-Object System.Collections.ArrayList }
        # approximate line number of the citation (count newlines before the match)
        $line = ($text.Substring(0, $mm.Index) -split "`n").Count
        [void]$cited[$name].Add($relDoc + ':' + $line)
    }
}
# Evidence roots: a citation may point at shell/Tests/evidence, at a per-screen <dir>/evidence, or at
# kernel/evidence (kernel2 keeps its evidence there). The first version only knew the central dir, which turned
# every kernel-evidence citation into a false MISSING -- build a NAME -> PATH index over every "evidence" dir.
$evidenceIndex = @{}
$evidenceDirs = @(Get-ChildItem -Recurse -Directory -Path '.' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'evidence' -and $_.FullName -notmatch '\\(bin|obj|\.git)\\' })
foreach ($d in $evidenceDirs) {
    foreach ($f in (Get-ChildItem -File -Path $d.FullName -ErrorAction SilentlyContinue)) {
        if (-not $evidenceIndex.ContainsKey($f.Name)) { $evidenceIndex[$f.Name] = (Get-RelPath $f.FullName) }
    }
}
Add-Note ("evidenceIndex: dirs=" + $evidenceDirs.Count + " files=" + $evidenceIndex.Count + " (covers kernel/evidence and per-screen evidence/ dirs)")

# ------------------------------------------------ H4 producer-side: .log under any evidence dir (advisory)
# ui3 traced the real root cause: the CAPTURE DRIVER itself wrote .log (now fixed at the source). So the gate
# watches the producer side. It is a NOTE, never a FAIL: 5 historical .log files are "cited => git add -f"
# grandfathered, and renaming them breaks citations (8 broken links tonight came from half-done renames).
$h4Scanned = 0
$h4Logs = New-Object System.Collections.ArrayList
$evidenceScanDirs = New-Object System.Collections.ArrayList
foreach ($d in $evidenceDirs) { [void]$evidenceScanDirs.Add($d.FullName) }
if ($ExtraEvidencePath -ne '') {
    foreach ($p in $ExtraEvidencePath.Split(';')) {
        if ($p.Trim() -ne '' -and (Test-Path -LiteralPath $p.Trim())) { [void]$evidenceScanDirs.Add((Resolve-Path -LiteralPath $p.Trim()).Path) }
    }
}
foreach ($dir in $evidenceScanDirs) {
    foreach ($f in (Get-ChildItem -Recurse -File -Path $dir -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git)\\' })) {
        $h4Scanned++
        if ($f.Extension -eq '.log') {
            $rel = Get-RelPath $f.FullName
            $inHead = $false
            $o = & git cat-file -e ("HEAD:" + $rel) 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) { $inHead = $true }
            [void]$h4Logs.Add(@{ Path = $rel; InHead = $inHead; Bytes = $f.Length })
        }
    }
}
$h4Grand = @($h4Logs | Where-Object { $_.InHead }).Count
$h4New = @($h4Logs | Where-Object { -not $_.InHead }).Count
Add-Check 'H4-evidence-log-extension' 'PASS' ("scannedEvidenceFiles=" + $h4Scanned + " logs=" + $h4Logs.Count + " historical(in HEAD)=" + $h4Grand + " new=" + $h4New) 'new evidence uses .txt; historical .log files are grandfathered (they are cited and tracked)' 'advisory only: never FAIL. Renaming a historical .log breaks citations, so the gate warns at the PRODUCER side instead'
foreach ($l in $h4Logs) {
    if ($l.InHead) { Write-Output ("INFO|H4|historical-evidence-log|cited-and-tracked-grandfathered|" + $l.Path + "|bytes=" + $l.Bytes) }
    else { Write-Output ("NOTE|H4|evidence-log-extension|" + $l.Path + "|bytes=" + $l.Bytes + "|advice=.txt is mandatory for NEW evidence (ui3 fixed the driver that produced these)") }
}

$untrackedCited = New-Object System.Collections.ArrayList
$missingCited = New-Object System.Collections.ArrayList
$missingFragments = New-Object System.Collections.ArrayList
$deletedInWorktree = New-Object System.Collections.ArrayList
foreach ($name in $cited.Keys) {
    # a citation may resolve next to its own document (that is where ui2/ui3 keep their evidence) or in the
    # central evidence dir -- try the candidates in that order and keep the first that exists.
    $cands = New-Object System.Collections.ArrayList
    foreach ($ref in $cited[$name]) {
        $docRel = ($ref -replace ':\d+$', '')
        $docDir = Split-Path -Parent $docRel
        if ($docDir -ne '') {
            [void]$cands.Add(($docDir.TrimEnd('/') + '/' + $name))
            [void]$cands.Add(($docDir.TrimEnd('/') + '/evidence/' + $name))
        }
    }
    [void]$cands.Add(($EvidenceDir.TrimEnd('/') + '/' + $name))
    if ($evidenceIndex.ContainsKey($name)) { [void]$cands.Add($evidenceIndex[$name]) }   # any */evidence/ dir (incl. kernel)
    $resolved = ''
    foreach ($c in $cands) { if (Test-Path -LiteralPath $c) { $resolved = $c; break } }
    if ($resolved -eq '') {
        # a file may be tracked in HEAD but deleted in the working tree (someone is cleaning up): that is a
        # pending-deletion state, not a broken citation -- report it as a note instead of "missing".
        $inHead = ''
        foreach ($c2 in $cands) {
            $o = & git cat-file -e ("HEAD:" + $c2) 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) { $inHead = $c2; break }
        }
        if ($inHead -ne '') { [void]$deletedInWorktree.Add(@{ Name = $name; Path = $inHead; Refs = ($cited[$name] -join ',') }); continue }
        $looksComplete = ($name -match '\.[A-Za-z0-9]{1,8}$')
        if ($looksComplete) { [void]$missingCited.Add($name) } else { [void]$missingFragments.Add($name) }
        continue
    }
    $t = Test-GitTracked $resolved
    if (-not $t.Tracked) {
        $ignored = Test-GitIgnored $resolved
        [void]$untrackedCited.Add(@{ Name = $name; Path = $resolved; Ignored = $ignored; Refs = ($cited[$name] -join ',') })
    }
}
# H2a = the captain's criterion: cited AND not in version control => FAIL
# captain 2026-09-12 (#5): the ignore rule itself changed (`!**/evidence/**/*.log`), so a bare count is NOT
# comparable across time. Every H2a reading therefore carries the identity of .gitignore at sample time, and
# keeps "untracked because ignored" separate from "untracked for another reason" (two different fixes).
$giId = 'no-.gitignore'
if (Test-Path -LiteralPath '.gitignore') {
    $gib = [System.IO.File]::ReadAllBytes('.gitignore')
    $gisha = (Get-FileHash -LiteralPath '.gitignore' -Algorithm SHA256).Hash.Substring(0, 12)
    $gimt = (Get-Item -LiteralPath '.gitignore').LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
    $giId = ('sha12=' + $gisha + ' bytes=' + $gib.Length + ' mtime=' + $gimt)
}
Add-Note ('H2a ignore-rule identity (sample-time): ' + $giId)
$igTrue = @($untrackedCited | Where-Object { $_.Ignored }).Count
$igFalse = @($untrackedCited | Where-Object { -not $_.Ignored }).Count
$h2aMeasured = ("citedNames=" + $cited.Count + " untracked=" + $untrackedCited.Count + " ignoredTrue=" + $igTrue + " ignoredFalse=" + $igFalse + " gitignore=" + $giId)
$h2aNote = ('sample-time .gitignore identity is printed so readings stay comparable; ignoredTrue => the file is swallowed by a rule (fix: rule or git add -f), ignoredFalse => it is simply not added yet (fix: git add). central dir: ' + $EvidenceDir)
if ($untrackedCited.Count -eq 0) {
    Add-Check 'H2a-cited-evidence-tracked' 'PASS' $h2aMeasured 'every cited evidence file is in version control' $h2aNote
}
else {
    Add-Check 'H2a-cited-evidence-tracked' 'FAIL' $h2aMeasured 'every cited evidence file is in version control' ('a committed doc citing a file that git does not track is not reproducible. ' + $h2aNote)
}
# H2b = cited but absent on disk: reported, but owned by the citing document (verdict INCONCLUSIVE, not FAIL)
if ($missingCited.Count -eq 0) {
    Add-Check 'H2b-cited-evidence-present' 'PASS' ("citedNames=" + $cited.Count + " missing=0") 'every cited evidence file exists on disk' 'searched per-doc dir, per-doc evidence/ dir, and the central dir; WRITING RULE (decided 2026-09-12): a name that is only MENTIONED (not cited) must be written as a bare name or in backticks WITHOUT a directory prefix -- anything shaped like evidence/<name> is read as a citation, and when it is absent this check turns INCONCLUSIVE (the citing document owns the fix; the codebase is not failing)'
}
else {
    Add-Check 'H2b-cited-evidence-present' 'INCONCLUSIVE' ("citedNames=" + $cited.Count + " missing=" + $missingCited.Count) 'every cited evidence file exists on disk' 'owner of the citing document must fix the citation (outside this tool). POLICY (kernel2 proposed, reviewer re-computed, decided here 2026-09-12): this stays INCONCLUSIVE, never FAIL -- a FAIL keyed on MISSING text would also fire on documents that quote this tool own FINDING|H2b line (self-reference trap #5). If that ever changes: FAIL only for (captured by the evidence/ regex) AND (absent on disk); every non-citation shape (bare backticked name, |MISSING| prefix, explicitly retired, directory prefix with a split code span) is a NOTE'
}
foreach ($u in $untrackedCited) {
    Write-Output ("FINDING|H2a|UNTRACKED|" + $u.Path + "|ignored=" + $u.Ignored + "|citedBy=" + $u.Refs)
}
foreach ($mn in $missingCited) {
    Write-Output ("FINDING|H2b|MISSING|" + $mn + "|citedBy=" + (($cited[$mn]) -join ','))
}
foreach ($frag in $missingFragments) {
    Write-Output ("NOTE|H2|fragment-not-a-file|" + $frag + "|citedBy=" + (($cited[$frag]) -join ','))
}
foreach ($dl in $deletedInWorktree) {
    Write-Output ("NOTE|H2|tracked-in-HEAD-but-deleted-in-worktree|" + $dl.Path + "|citedBy=" + $dl.Refs)
}
foreach ($n in $notes) { Write-Output ("NOTE|" + $n) }

# ---------------------------------------------------------------- H3 empty catch (whole-file MULTILINE regex)
# Method note (captain 2026-09-12): a line-level regex MISSES multi-line `catch\n{\n}`. The criterion must run
# over the WHOLE FILE with RegexOptions.Multiline/Singleline (here: (?s) + [\s\S] semantics via Singleline).
# The captain's own rescan of shell/** gave 14 (fewer than the earlier 16/17 because the owners fixed theirs) --
# the method is decisive, but the number must be measured per surface and never extrapolated.
$catchHits = New-Object System.Collections.ArrayList
$broadIgnored = New-Object System.Collections.ArrayList
$csFiles = @()
foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $csFiles += @(Get-ChildItem -Recurse -File -Path $root -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -eq '.cs' -and $_.FullName -notmatch '\\(bin|obj|reversed)\\' })
}
foreach ($f in $csFiles) {
    $rel = Get-RelPath $f.FullName
    $text = [System.IO.File]::ReadAllText($f.FullName)
    # strip comments so a commented-out catch (or a quoted Dart snippet like `catch (_) {}` in a comment)
    # does not count: line comments first, then block comments.
    $textNoLineComments = ($text -split "`n" | ForEach-Object { ($_ -replace '//.*$', '') }) -join "`n"
    $textNoComments = [regex]::Replace($textNoLineComments, '/\*[\s\S]*?\*/', '')
    $lineArr = $text -split "`n"   # RAW lines: the lookback needs the comments (they are stripped in the scan copy)
    # t261: MATCH THE RAW TEXT, NOT THE COMMENT-STRIPPED COPY.
    # Two predecessor bugs came from matching the stripped copy, and both are preprocessing bugs that no
    # amount of pattern-tuning exposes:
    #   (1) the old `\{\s*\}` matched only a whitespace body, so an in-body reason (`{ // r }` or
    #       `{ /* r */ }`) matched NOTHING -- the site was neither an empty catch nor a recorded one;
    #   (2) matching the stripped copy also STRIPS a trailing `}   // note` down to `}`, and strips an in-body
    #       `//` reason to nothing -- so bodies shifted and were then read from the WRONG source. Measured
    #       2026-09-12: my first cut of this edit reported emptyCatch=311 on shell/** (it even called
    #       `catch (Exception) { return; }` an EMPTY catch) against 0 before the edit.
    # RULE: `$lineArr` (raw) is the source of truth for the MATCH and the BODY; the stripped copy exists only
    # to make the whole-file scan insensitive to commented-out code. A statement body is NOT this check's
    # subject at all -- only a body with no statements can be an empty catch.
    foreach ($mm in [regex]::Matches($text, 'catch\s*(\([^)]*\))?\s*\{([^{}]*)\}')) {
        $line = ($text.Substring(0, $mm.Index) -split "`n").Count
        $body = $mm.Groups[2].Value
        $bodyNoComments = [regex]::Replace($body, '/\*[\s\S]*?\*/', '')
        $bodyNoComments = ($bodyNoComments -replace '//.*$', '')
        $bodyIsBare = ($bodyNoComments.Trim() -eq '')
        $bodyCommentOnly = ($body.Trim() -ne '') -and $bodyIsBare
        if (-not $bodyIsBare) {
            # The body carries statements. Skip: this check counts catches with NO statements.
            # (Kept as a `continue`, not a silent drop: see the t261 evidence file -- statement bodies were the
            # 311-site false positive of the first cut.)
            continue
        }
        # compliant form (captain's 2026-09-12 refinement): the reason may sit in the same statement, OR on the line
        # right above the whole try/catch statement (e.g. Spike/Probe.cs writes the reason above `try { ... }`).
        # So: walk back a few lines, tolerating blank lines and the `try` line itself.
        # captain 2026-09-12 (#6-1): the reason may also be a TRAILING comment on the catch line ITSELF
        # (`catch (IOException) { }   // intentional-ignore: ...`). The old lookback started at the line ABOVE
        # and never looked at the catch line, so 13 legitimate typed sites were reported -- a false positive in
        # my own criterion, not in their code.  (Kept ASCII-only on purpose: PS 5.1 decodes a BOM-less script as
        # ANSI, so CJK in a comment can be mangled and even swallow the following newline.)
        $compliant = $false
        if ($bodyCommentOnly) { $compliant = $true }              # t261: reason lives IN the body
        $ownLine = $lineArr[$line - 1]
        if (-not $compliant -and ($ownLine -match '//' -or $ownLine -match '/\*')) { $compliant = $true }
        for ($k = $line - 2; -not $compliant -and $k -ge 0 -and $k -ge ($line - 6); $k--) {
            $cand = $lineArr[$k].Trim()
            if ($cand -eq '') { continue }
            if ($cand.StartsWith('//') -or $cand.StartsWith('/*') -or $cand.StartsWith('*')) { $compliant = $true; break }
            if ($cand.StartsWith('try')) { continue }
            if ($cand.StartsWith('catch')) { continue }   # a try/catch CHAIN shares one reason above the try
            break
        }
        $isTyped = ($mm.Value -match 'catch\s*\(')
        $typeName = ''
        if ($isTyped) {
            $mt = [regex]::Match($mm.Value, 'catch\s*\(\s*([A-Za-z_][A-Za-z0-9_\.]*)')
            if ($mt.Success) { $typeName = $mt.Groups[1].Value }
        }
        $lastSeg = ''
        if ($typeName -ne '') { $lastSeg = @($typeName.Split('.'))[-1] }
        # CODE_STANDARD 5-3 only allows ignoring a NARROW type: an empty `catch (Exception ex) { }` is the shape
        # that hides everything, so a reason does not rescue a BROAD type -- it is reported as an advisory, not
        # passed silently ("typed => always pass" is exactly the trap this avoids).
        $isBroad = ($lastSeg -eq 'Exception' -or $lastSeg -eq 'SystemException')
        if ($compliant) {
            if ($isBroad) {
                [void]$broadIgnored.Add(@{ Path = $rel; Line = $line; Text = ($mm.Value -replace '\s+', ' '); Type = $typeName })
            }
            continue
        }
        [void]$catchHits.Add(@{ Path = $rel; Line = $line; Text = ($mm.Value -replace '\s+', ' '); Typed = $isTyped; Broad = $isBroad })
    }
}
if ($catchHits.Count -eq 0) {
    Add-Check 'H3-empty-catch' 'PASS' ("csFiles=" + $csFiles.Count + " emptyCatch=0 (untyped=0 typed=0)" + " broad-ignored=" + $broadIgnored.Count) 'whole-file multiline regex catches nothing on a NON-EMPTY face; broad-ignored reports the no-content bodies that a reason rescues' 'criterion = catch over the whole file with the BODY CAPTURED, judged on the RAW line so an in-body comment is seen as a reason (t261); a line-level scan misses multi-line forms'
}
else {
    $untyped = @($catchHits | Where-Object { -not $_.Typed }).Count
    $typed = @($catchHits | Where-Object { $_.Typed }).Count
    Add-Check 'H3-empty-catch' 'INCONCLUSIVE' ("csFiles=" + $csFiles.Count + " emptyCatch=" + $catchHits.Count + " (untyped=" + $untyped + " typed=" + $typed + ")" + " broad-ignored=" + $broadIgnored.Count) 'owners fix their own files (CODE_STANDARD G2); captain measured shell/** = 14 untyped at 2026-09-12' 'report-only here: the fix belongs to each file owner; the criterion is the whole-file multiline regex with the BODY CAPTURED and judged on the RAW line (t261); an empty body counts only when NO reason sits in the body, on the catch line, or on the line(s) above'
}
foreach ($h in $catchHits) {
    Write-Output ("FINDING|H3|EMPTY-CATCH|" + $h.Path + ":" + $h.Line + "|" + $h.Text)
}
foreach ($b in $broadIgnored) {
    Write-Output ("NOTE|H3|broad-type-ignored|" + $b.Path + ":" + $b.Line + "|" + $b.Text + "|type=" + $b.Type + "|advice=CODE_STANDARD 5-3 only allows ignoring a NARROW type; a reason on a broad type does not rescue it (advisory: this line is NOT counted as a finding)")
}

# ---------------------------------------------------------------- H3b the REAL hazard: a BARE catch
# The shape H3 matched was "empty body"; the hazard is "no exception type" -- `catch { }` and
# `catch { /* ignore */ }` both swallow everything, and the commented one is WORSE (it pretends to explain).
# captain 2026-09-12: any catch without an exception type => FAIL, regardless of the body. A reason INSIDE the
# braces does not count either; compliance = narrow type (+ optional when(...) filter) AND a reason in the same
# statement or on the immediately preceding line (same lookback as H3).
$bareHits = New-Object System.Collections.ArrayList
foreach ($f in $csFiles) {
    $rel = Get-RelPath $f.FullName
    $text = [System.IO.File]::ReadAllText($f.FullName)
    # captain 2026-09-12 (ruling #2, after `services` found his contradiction): H3b judges exactly ONE thing --
    # a catch that does not NAME an exception type. A narrow type + a reason on the same line (whether the
    # reason sits inside the braces or trails them) or on the immediately preceding line is COMPLIANT
    # (CODE_STANDARD 5-3). REASON-INSIDE-BRACES therefore applies ONLY to a BARE catch whose body is nothing
    # but a comment -- `catch { /* ignore */ }`, which LOOKS like it explains while still swallowing everything.
    # A TYPED catch is never classified here. (The previous build put typed+inline-comment here: 9 false
    # positives, all legitimate sites in shell/Services.)
    # Comments are BLANKED (same length, newlines kept => same indices and same line numbers) rather than
    # deleted, so a `catch {` inside a comment (or a quoted Dart `catch (_) {}`) stays invisible while an
    # INLINE comment inside a real body is still visible for the classification below.
    $masked = [regex]::Replace($text, '(?s)/\*.*?\*/', { param($m) [regex]::Replace($m.Value, '[^\n]', ' ') })
    $masked = [regex]::Replace($masked, '//[^\n]*', { param($m) ' ' * $m.Value.Length })
    $lineArr = $text -split "`n"
    foreach ($mm in [regex]::Matches($masked, 'catch\s*\{')) {
        $line = ($masked.Substring(0, $mm.Index) -split "`n").Count
        # body span by brace depth; indices are valid in BOTH texts because masking preserves length
        $open = $mm.Index + $mm.Length - 1
        $depth = 0; $end = -1
        for ($j = $open; $j -lt $masked.Length; $j++) {
            $ch = $masked[$j]
            if ($ch -eq '{') { $depth++ }
            elseif ($ch -eq '}') { $depth--; if ($depth -eq 0) { $end = $j; break } }
        }
        $kind = 'BARE'
        if ($end -gt $open) {
            $rawBody = $text.Substring($open + 1, $end - $open - 1)
            $codeOnly = $masked.Substring($open + 1, $end - $open - 1)
            if ($rawBody -match '(/\*|//)' -and $codeOnly.Trim() -eq '') { $kind = 'REASON-INSIDE-BRACES' }
        }
        $shown = 'catch {'
        if ($kind -eq 'REASON-INSIDE-BRACES' -and $line -ge 1 -and $line -le $lineArr.Count) { $shown = $lineArr[$line - 1].Trim() }
        [void]$bareHits.Add(@{ Path = $rel; Line = $line; Kind = $kind; Text = $shown; Mtime = $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') })
    }
}
$bareOnly = @($bareHits | Where-Object { $_.Kind -eq 'BARE' }).Count
$reasonInside = @($bareHits | Where-Object { $_.Kind -eq 'REASON-INSIDE-BRACES' }).Count
# RATCHET (captain 2026-09-12): the existing bare-catch stock is recorded as a baseline and does NOT fail;
# the gate fails only on NEW instances. Rationale: many of the 54 are mandatory top-level handlers (process
# entry / callback endpoints) where `catch (Exception ex)` + a reason is a mechanical, legitimate fix -- but
# demanding all 54 at once would block five in-flight cards. Same "measure first, forbid only new" policy as
# EnforceCodeStyleInBuild on the kernel. The baseline may only DECREASE.
# owner attribution for the work list: ownership CANNOT be derived from a path, so it is never guessed --
# it comes from a small dated map file that a human edits when the captain re-assigns work. Unknown => UNMAPPED.
$ownerMap = New-Object System.Collections.ArrayList
if (Test-Path -LiteralPath $OwnerMapFile) {
    foreach ($ln in [System.IO.File]::ReadAllLines($OwnerMapFile)) {
        $t = $ln.Trim()
        if ($t -eq '' -or $t.StartsWith('#')) { continue }
        $parts = $t -split '\s+', 2
        if (@($parts).Count -lt 2) { continue }
        [void]$ownerMap.Add(@{ Rx = $parts[0]; Owner = $parts[1].Trim() })
    }
}
foreach ($h in $bareHits) {
    $own = 'UNMAPPED'
    foreach ($m in $ownerMap) { if ($h.Path -match $m.Rx) { $own = $m.Owner; break } }
    $h.Owner = $own
}
$byOwner = (@($bareHits | ForEach-Object { $_.Owner } | Group-Object | Sort-Object Name | ForEach-Object { $_.Name + '=' + $_.Count }) -join ',')
if ($byOwner -eq '') { $byOwner = 'none' }
# captain 2026-09-12 ruling B: the ratchet now measures PRODUCTION counts; in-repo probe/fixture hits keep
# their own quota (they must not grow either, but they do not consume production headroom). Totals are still
# printed for completeness -- never gate on them again.
$bareProd = @($bareHits | Where-Object { $_.Kind -eq 'BARE' -and $_.Owner -ne 'UNMAPPED' }).Count
$bareUnmapped = @($bareHits | Where-Object { $_.Kind -eq 'BARE' -and $_.Owner -eq 'UNMAPPED' }).Count
$ribProd = @($bareHits | Where-Object { $_.Kind -eq 'REASON-INSIDE-BRACES' -and $_.Owner -ne 'UNMAPPED' }).Count
$ribUnmapped = @($bareHits | Where-Object { $_.Kind -eq 'REASON-INSIDE-BRACES' -and $_.Owner -eq 'UNMAPPED' }).Count
$newProdBare = [Math]::Max(0, $bareProd - $H3bProdBareBaseline)
$newProdRib = [Math]::Max(0, $ribProd - $H3bProdRibBaseline)
$newUnmappedBare = [Math]::Max(0, $bareUnmapped - $H3bUnmappedBareCap)
$newUnmappedRib = [Math]::Max(0, $ribUnmapped - $H3bUnmappedRibCap)
# TIME IDENTITY (services 2026-09-12, after the THIRD "your sample is older than the fix" incident):
# a gate reading must say WHEN it read and HOW NEW the newest read file was, exactly like H5 says which
# file it hashed. Without that, "H3b is red" cannot be told apart from "H3b was red before the owner fixed
# it" -- and the second reading is not evidence about the current source at all. This adds NO new rule: it
# only makes the existing verdict self-dating (captain's fact 216: every reading carries its own identity).
$newestCsMtime = '<none>'
$newestStamp = [datetime]::MinValue
foreach ($cf in $csFiles) {
    if ($cf.LastWriteTime -gt $newestStamp) {
        $newestStamp = $cf.LastWriteTime
        $newestCsMtime = ((Split-Path $cf.FullName -Leaf) + '@' + $cf.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))
    }
}
$h3bMeasured = ("csFiles=" + $csFiles.Count + " bareCatchTotal=" + $bareOnly + " ribTotal=" + $reasonInside + " baselineAt=" + $H3bBaselineAt +
    " productionBare=" + $bareProd + " prodBaseline=" + $H3bProdBareBaseline + " newProdBare=" + $newProdBare +
    " productionRib=" + $ribProd + " prodRibBaseline=" + $H3bProdRibBaseline + " newProdRib=" + $newProdRib +
    " unmappedBare=" + $bareUnmapped + " unmappedBareCap=" + $H3bUnmappedBareCap + " newUnmappedBare=" + $newUnmappedBare +
    " unmappedRib=" + $ribUnmapped + " unmappedRibCap=" + $H3bUnmappedRibCap + " newUnmappedRib=" + $newUnmappedRib +
    " byOwner=" + $byOwner + " scannedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + " newestScannedMtime=" + $newestCsMtime)
$h3bTotalNew = $newProdBare + $newProdRib + $newUnmappedBare + $newUnmappedRib
# captain 2026-09-12: the two groups ARE the total (total = production + unmapped), so gating each group at its
# own baseline is equivalent to gating the total -- but print the total pair explicitly so nobody has to derive it.
$h3bTotalBaseline = ($H3bProdBareBaseline + $H3bUnmappedBareCap) + ($H3bProdRibBaseline + $H3bUnmappedRibCap)
$h3bMeasured = $h3bMeasured + " totalBaselineBare=" + ($H3bProdBareBaseline + $H3bUnmappedBareCap) + " newTotalBare=" + ($newProdBare + $newUnmappedBare) + " totalBaselineRib=" + ($H3bProdRibBaseline + $H3bUnmappedRibCap) + " newTotalRib=" + ($newProdRib + $newUnmappedRib) + " newTotal=" + $h3bTotalNew
# captain 2026-09-12 (2nd item into t216, ZERO-SEMANTIC): print the two INPUTS of this measurement on the
# CHECK line itself. The four H3b baselines are GLOBAL (bareProd / newProdBare / csFiles all aggregate over
# every $scanRoots entry), so "the shell face uses the default 0 / the kernel face is scanned as a single root
# with an explicit baseline" was only a CONVENTION -- the script never said which face produced which number,
# and only the -Json output carried the inputs. Printing the face next to the baseline lets a reader check
# "which face, and was the baseline default or explicit" without having to remember. TEXT ONLY: no verdict
# branch, no checks= count, no scan face, no allowlist is touched.
$h3bExtraShown = '<none>'
if ($ExtraScanPath -ne '') { $h3bExtraShown = $ExtraScanPath }
$h3bMeasured = $h3bMeasured + " scanRoot=" + $ScanRoot + " extraScanPath=" + $h3bExtraShown

if ($h3bTotalNew -eq 0) {
    Add-Check 'H3b-bare-catch' 'PASS' $h3bMeasured 'no NEW production bare catch / reason-inside-braces (in-repo probe/fixture hits have their own quota)' ('RATCHET: THE MEASURED OBJECT IS PRODUCTION COUNTS (captain 2026-09-12 ruling B); in-repo probe/fixture hits keep a separate quota and never consume production headroom; totals are printed for completeness only and must not be gated on. Top-level handlers become compliant by naming the type, e.g. catch (Exception ex) + a reason. Human review (NOT this gate): on an async path that carries a CancellationToken, a catch must not swallow OperationCanceledException -- use when (ex is not OperationCanceledException) or rethrow explicitly')
}
else {
    Add-Check 'H3b-bare-catch' 'FAIL' $h3bMeasured 'no NEW production bare catch / reason-inside-braces (in-repo probe/fixture hits have their own quota)' ('a bare catch swallows every exception; a commented bare catch pretends to explain while still swallowing everything; NEW instances are what this gate forbids -- check newProdBare/newProdRib/newUnmappedBare/newUnmappedRib to see WHICH quota tripped')
}
foreach ($h in $bareHits) {
    $scope = 'production'
    if ($h.Owner -eq 'UNMAPPED') { $scope = 'unmapped' }
    Write-Output ("FINDING|H3b|" + $h.Kind + "|" + $h.Path + ":" + $h.Line + "|owner=" + $h.Owner + "|scope=" + $scope + "|fileMtime=" + $h.Mtime + "|" + $h.Text)
}
if ($bareUnmapped + $ribUnmapped -gt 0) {
    $unmPaths = (@($bareHits | Where-Object { $_.Owner -eq 'UNMAPPED' } | ForEach-Object { $_.Path } | Sort-Object -Unique) -join ',')
    Write-Output ("NOTE|H3b|unmapped-hits-not-in-the-production-stock|" + $unmPaths + "|advice=these sit in files the owner map does not cover (in-repo probe/fixture). They are split out of productionBare/productionRib so the reading is not misread as a production regression, but they DO still count in the ratchet (captain 2026-09-12: no exclusion rule; a fixture must itself be compliant or move to %TEMP%)")
}
# NOTE (not FAIL) for cited evidence that is still a .log: .txt is the mandatory form for NEW evidence, but the
# gate keeps "cited => must be tracked"; renaming history would break more citations than it fixes.
foreach ($name in $cited.Keys) {
    if ($name -match '\.log$') {
        Write-Output ("NOTE|H2|cited-evidence-is-log|" + $name + "|citedBy=" + (($cited[$name]) -join ',') + "|advice=.txt is mandatory for NEW evidence; for existing ones prefer owner-updates-reference-then-rename")
    }
}

# ---------------------------------------------------------------- H6 IRREVERSIBLE ACTION: kill by NAME
# captain 2026-09-12 fact 221 authorized this gate, and the finding that motivated it is mine (02:36):
# shell/App/tools/t26-evidence.ps1:60-66 ran `Get-Process -Name 'AIPlayer.Shell' | Stop-Process -Force`, i.e.
# it could end EVERY instance on the machine (proved by ui-t26-attempt3-evidence.txt:3 `KILL-STALE pid=15484`),
# while README-ui-audit.md:777 already stated the rule "a capture script may only stop the pid IT started".
# The subject is not style but an IRREVERSIBLE ACTION (kill / delete / write credentials) -- those need a
# mechanically scannable rule. Measured object = REAL hits; a pattern occurring only inside a quoted literal
# is a SEARCH-ONLY note (this tool itself searches for those strings). The captain's ruling quoted
# baseline=1 (the stock BEFORE the owner fixed t26-evidence.ps1); the stock is MEASURED and printed with
# baselineAt rather than assumed, and the ratchet rule is unchanged: the baseline may only decrease.
# BUGFIX (02:54:40 control A did NOT fire): the first rule set only looked for the two tokens on the SAME
# line, so it MISSED the very shape that motivated this gate --
#   Get-Process -Name 'AIPlayer.Shell' -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
# (the name is acquired on the first line, the kill happens inside the block). The criterion is therefore
# "a process SET acquired BY NAME, and a termination within the same statement window" -- not "the word -Name
# next to the word Stop-Process". The window is 5 following lines (measured: enough for the real sites, short
# enough not to couple unrelated code).
$H6Direct = @(
    @{ Kind = 'STOP-PROCESS-BY-NAME'; Rx = 'Stop-Process[^\r\n]*-Name\b' },
    @{ Kind = 'TASKKILL-IM'; Rx = 'taskkill[^\r\n]*/IM\b' }
)
$H6Acquire = @(
    @{ Kind = 'GET-PROCESS-BY-NAME'; Rx = 'Get-Process[^\r\n]*-Name\b' },
    @{ Kind = 'GETPROCESSESBYNAME'; Rx = 'GetProcess(es)?ByName\b' }
)
$H6Terminate = 'Stop-Process\b|\.Kill\s*\(|taskkill\b'
function Get-H6QuotedMask([string]$line) {
    $m = [regex]::Replace($line, '"[^"]*"', { param($x) ' ' * $x.Value.Length })
    $m = [regex]::Replace($m, "'[^']*'", { param($x) ' ' * $x.Value.Length })
    return $m
}
$h6Files = @()
foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $h6Files += @(Get-ChildItem -Recurse -File -Path $root -ErrorAction SilentlyContinue |
        Where-Object { ($_.Extension -eq '.ps1' -or $_.Extension -eq '.cs') -and $_.FullName -notmatch '\\(bin|obj|reversed)\\' })
}
$h6Hits = New-Object System.Collections.ArrayList
$h6SearchOnly = 0
foreach ($f in $h6Files) {
    $rel = Get-RelPath $f.FullName
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    $mt = $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        if (Test-CommentLine $ln) { continue }
        $masked = Get-H6QuotedMask $ln
        $kind = ''
        foreach ($r in $H6Direct) { if ($masked -match $r.Rx) { $kind = $r.Kind; break } }
        if ($kind -eq '') {
            $acq = ''
            foreach ($r in $H6Acquire) { if ($masked -match $r.Rx) { $acq = $r.Kind; break } }
            if ($acq -ne '') {
                for ($k = $i; $k -le [Math]::Min($i + 5, $lines.Count - 1); $k++) {
                    if ((Get-H6QuotedMask $lines[$k]) -match $H6Terminate) { $kind = $acq + '-THEN-STOP'; break }
                }
            }
        }
        if ($kind -eq '') {
            $rawHit = $false
            foreach ($r in $H6Direct) { if ($ln -match $r.Rx) { $rawHit = $true; break } }
            if (-not $rawHit) { foreach ($r in $H6Acquire) { if ($ln -match $r.Rx) { $rawHit = $true; break } } }
            if ($rawHit) { $h6SearchOnly++ }
            continue
        }
        [void]$h6Hits.Add(@{ Path = $rel; Line = ($i + 1); Kind = $kind; Text = $ln.Trim(); Mtime = $mt })
    }
}
foreach ($h in $h6Hits) {
    $own = 'UNMAPPED'
    foreach ($m in $ownerMap) { if ($h.Path -match $m.Rx) { $own = $m.Owner; break } }
    $h.Owner = $own
}
$byOwner6 = (@($h6Hits | ForEach-Object { $_.Owner } | Group-Object | Sort-Object Name | ForEach-Object { $_.Name + '=' + $_.Count }) -join ',')
if ($byOwner6 -eq '') { $byOwner6 = 'none' }
$h6Head = ''
try { $h6Head = ((& git -C $repoRoot rev-parse --short HEAD 2>$null) | Select-Object -First 1) } catch { $h6Head = '<git failed>' }
$h6New = [Math]::Max(0, $h6Hits.Count - $H6BatchKillBaseline)
$h6Measured = ("scannedFiles=" + $h6Files.Count + " realHits=" + $h6Hits.Count + " searchOnly=" + $h6SearchOnly +
    " baseline=" + $H6BatchKillBaseline + " baselineAt=" + $H6BaselineAt + " new=" + $h6New +
    " byOwner=" + $byOwner6 + " scannedAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + " head=" + $h6Head)
if ($h6New -eq 0) {
    Add-Check 'H6-batch-kill-by-name' 'PASS' $h6Measured 'no script may end processes it did not start; ending processes BY NAME is a batch action over other people instances' ('IRREVERSIBLE ACTION, not style (captain fact 221). The compliant shape is: keep the object returned by your own Start-Process / Process.Start and stop only that pid. Needing to clear a stale instance is NOT a licence to kill: report the count (path + pid + start time) and let the owner react. Baseline may only decrease')
}
else {
    Add-Check 'H6-batch-kill-by-name' 'FAIL' $h6Measured 'no script may end processes it did not start; ending processes BY NAME is a batch action over other people instances' ('a kill by process NAME can end a teammate window, a user session or another agent evidence run in flight -- and it is IRREVERSIBLE. Stop only the pid your own Start-Process returned, or print a STALE-PRESENT report and exit instead of killing')
}
foreach ($h in $h6Hits) {
    Write-Output ("FINDING|H6|" + $h.Kind + "|" + $h.Path + ":" + $h.Line + "|owner=" + $h.Owner + "|fileMtime=" + $h.Mtime + "|" + $h.Text)
}
if ($h6SearchOnly -gt 0) {
    Write-Output ("NOTE|H6|search-only-literal|count=" + $h6SearchOnly + "|advice=these lines only SEARCH for the pattern (the token sits inside a quoted literal); they are not kills and are not counted")
}

# NOTE (BUGFIX 2026-09-12 03:21): the counts used to be computed HERE, i.e. BEFORE the H5 block below added
# its own CHECK -- so H5's FAIL never reached $overall/$exit and never appeared in pass/fail/inconclusive
# (checks=9 while pass+fail+inc=8). Measured live: H5 reported mismatch=2 while SUMMARY said PASS/EXIT=0.
# A gate whose newest check cannot fail the gate is worse than no check: it prints FAIL and exits green.
# The counts are now computed AFTER every Add-Check call (just before the SUMMARY line).
# ---------------------------------------------------------------- H5 evidence identity (bytes + sha12)
# "cited and tracked" (H2a) only proves the file EXISTS; it does not prove the file is the one the document
# claims. H5 checks the identity a document states for a file: bytes and the UPPERCASE sha256 first 12 hex.
# Hazard class #5 of "the shape is not the hazard": there was no check at all for "the doc says a false hash".
$h5Rows = 0
$h5Mismatch = New-Object System.Collections.ArrayList
$h5Absent = New-Object System.Collections.ArrayList
$h5Lower = New-Object System.Collections.ArrayList
$h5Bare = New-Object System.Collections.ArrayList
$h5Docs = @()
foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    # NOTE: no -Include here on purpose. `-Include` with a non-wildcard -Path is provider-version dependent
    # (it can silently return EVERY file); the explicit name filter below is what actually selects the docs,
    # so it is the only thing the result depends on.
    $h5Docs += @(Get-ChildItem -Recurse -File -Path $root -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '*EVIDENCE*.md' -and $_.FullName -notmatch '\\(bin|obj)\\' })
}
# t301 B: DEDUP the H5 document face by NORMALIZED FULL PATH.
# WHY: $h5Docs is accumulated once per $scanRoots entry, and $scanRoots = ScanRoot + every ExtraScanPath entry.
# Passing the same root twice (or nested/overlapping roots, e.g. -ScanRoot shell -ExtraScanPath shell/App) put the
# same document into the face MORE THAN ONCE. MEASURED before this fix: one probe root passed to both -ScanRoot and
# -ExtraScanPath made a 2-row document report bareRows=4 and printed every advisory twice -- one document counted
# as two, silently INFLATING scannedRows / bareRows / mismatch for any caller whose roots overlap.
# The fix is about the IDENTITY of a document (normalized full path, case-insensitive) only: no check, no counter
# definition, no face and no allowlist changes. Two GENUINELY different roots still contribute their own rows.
$h5Seen = New-Object 'System.Collections.Generic.HashSet[string]'
$h5Docs = @($h5Docs | Where-Object { $h5Seen.Add(([IO.Path]::GetFullPath($_.FullName)).ToLowerInvariant()) })
# t216 CELL SELECTION for identity rows. Measured 2026-09-12: the old rule took the row's FIRST numeric cell
# as the DECLARED BYTES, so a markdown table's index column (`| 2 | ... |`, or a `hwnd / pid` cell whose value
# starts with a digit) stole the bytes slot and the row then failed HASH-MISMATCH against a file whose real bytes
# were never that cell. Two live instances: a thousands-separated `60,908` row, and `EVIDENCE_T102.md:29` (a
# change-table row) which the synthetic control turned into `expectedBytes=3` + H5 FAIL once its bare name was
# rewritten as a full path -- i.e. the advisory's own advice produced a hard red.
# RULE (t216 A2): strip the empty cells that markdown pipes add, then NEVER take the first logical column;
# the bytes scan starts at the second logical column. sha12 selection is unchanged (a 12-hex cell is unambiguous).
# REFINEMENT (t238 A) -- WHAT THIS ACTUALLY BUYS (the motivating claim was FALSIFIED, do not repeat it).
# The t238 plan claimed: "a row whose bytes cell sits in the FIRST logical column yields no bytes cell, so
# H5 never compares it and a stale byte count hides forever". MEASURED FALSE (2026-09-12): A2 starts the scan
# at (first non-empty cell)+1, i.e. AFTER the markdown LABEL column, and the first logical column of a real
# identity row is the label -- so the old rule already read `| 8510 | <path> | <sha12> |` correctly. Evidence:
# a gate-level control over 12 single-row documents (old binary vs this file, same corpus) gave
# scannedRows=74 BOTH times; the old rule produced TWO EXTRA REDS, both false, and no missed row.
# WHAT THE OLD RULE GOT WRONG (the two false reds this refinement removes):
#   X6 `| <path> | 3017916 / 16404 | 8,510 | <sha12> |`  old expectedBytes=3017916 (raw hwnd/pid)
#   X8 `| 4 | <path> | 8,510 | <sha12> |`                old expectedBytes=4        (numeric index label)
# The rule is therefore: pick the cell the row LABELS as bytes -- not "close a blind spot".
#   1. candidates = cells after the first non-empty cell whose text is ^[0-9][0-9,]* followed by `/` or EOL
#      (=> `6,618 / 85` qualifies, `01:55:43.798` does NOT);
#   2. if there are no candidates and the first logical cell itself looks like bytes, take it (A2 returned null);
#   3. EXACTLY ONE candidate -> take it. This is what keeps a digit-leading sha12 from being selected:
#      `3F345754D131` matches the bytes pattern on its first character, so a position-only rule ("last
#      candidate before the sha cell") hands back the SHA CELL and reports expectedBytes=3. My first cut of
#      this change did exactly that and the control corpus caught it -- correct rows went red. Any sha-relative
#      rule MUST exclude the sha index explicitly (see step 4 and the guard below).
#   4. several candidates + a declared sha12 -> LAST candidate positioned BEFORE the sha12 cell; if none
#      precedes it, the first candidate OTHER THAN the sha12 cell itself;
#   5. never return the sha12 cell.
# RESIDUAL (declared, NOT fixed -- see README-ui-audit.md 12.5.4.1):
#   (a) `| <path> | 3017916 / 16404 | <sha12> |` -- a raw numeric hwnd with NO bytes column is byte-for-byte
#       indistinguishable from a legitimate `6,618 / 85` bytes/lines cell; both old and new rules read the
#       hwnd. The remedy is on the WRITING side: write `hwnd=3017916 / pid=16404` (ui3's t197 remediation).
#   (b) `| 2 | note | 8,510 | <sha12> |` (numeric index left, bytes later, sha last) still reads the index;
#       string-level indistinguishable from `| 8,510 | <path> | <sha12> |`, Sigma=0 in the 128-row evidence face.
# The call sites now compute the sha12 cell FIRST and pass it in; sha extraction itself is unchanged.
function Get-H5BytesCell($cells, $shaCell) {
    $lo = 0
    while ($lo -lt $cells.Count -and $cells[$lo] -eq '') { $lo++ }
    $cands = @()
    for ($j = $lo + 1; $j -lt $cells.Count; $j++) {
        if ($cells[$j] -match '^\s*([0-9][0-9,]*)\s*(/|\z)') { $cands += ,@($j, $cells[$j]) }
    }
    if ($cands.Count -eq 0) {
        if ($lo -lt $cells.Count -and $cells[$lo] -match '^\s*([0-9][0-9,]*)\s*(/|\z)') { return $cells[$lo] }
        return $null
    }
    $si = -1
    if ($null -ne $shaCell) {
        for ($j = 0; $j -lt $cells.Count; $j++) {
            if ($cells[$j] -eq [string]$shaCell) { $si = $j; break }
        }
    }
    # NEVER HAND BACK THE SHA12 CELL ITSELF. A 12-hex sha12 whose first char is a digit ALSO matches the
    # bytes pattern on its first character (`3F345754D131` -> `3`), so a row like
    # `| <path> | 8,510 | `3F345754D131` |` offers TWO candidates and positions the sha one LAST. Selecting it
    # as "bytes" then compares 3 against the real file length. The pre-t238 rule never hit this because it
    # took the FIRST candidate; any sha-relative rule must exclude the sha index explicitly. (Found by the
    # t238 gate-level control: with the sha cell counted, correct rows went INVISIBLE or reported
    # expectedBytes=3.)
    $pick = -1
    if ($si -ge 0) {
        for ($k = $cands.Count - 1; $k -ge 0; $k--) {
            if ($cands[$k][0] -lt $si) { $pick = $k; break }
        }
    }
    if ($pick -lt 0) {
        for ($k = 0; $k -lt $cands.Count; $k++) {
            if ($cands[$k][0] -ne $si) { $pick = $k; break }
        }
    }
    if ($pick -lt 0) { return $null }
    return $cands[$pick][1]
}

foreach ($doc in $h5Docs) {
    $docRel = Get-RelPath $doc.FullName
    $docDir = Split-Path -Parent $docRel
    $lines = [System.IO.File]::ReadAllLines($doc.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '(?i)evidence[/\\]([A-Za-z0-9._\-]+\.[A-Za-z0-9]{2,5})') {
            # BARE-NAME IDENTITY ROW (2026-09-12; gap found by ui2 while fixing the t31 evidence tables).
            # The matcher above requires an 'evidence/<name>' substring, so an identity row written with a BARE
            # filename sits OUTSIDE the scan face entirely -- "the doc states a stale hash" can then hide for a
            # long time. Live example measured 2026-09-12 17:4x: t31-U-E-EVIDENCE.md line 417 declared
            # C8AB14D320B9 for t31-favorites-selftest.txt while the artifact on disk is C565252A8DBD (bytes and
            # lines still matched, so the row looked healthy). ADVISORY only -- the row may be a deliberate
            # historical reading; the advice is to write the full path so H5 can actually compare it.
            $bm = [regex]::Match($line, '`([A-Za-z0-9._\-]+\.(?:txt|log|png|json))`')
            if ($bm.Success) {
                $bcells = @($line.Split('|') | ForEach-Object { $_.Trim() })
                $bs = ($bcells | Where-Object { $_ -match '^`?[0-9A-Fa-f]{12}`?$' } | Select-Object -First 1)
                $bb = Get-H5BytesCell $bcells $bs   # t238 A: sha12 first, then bytes (see Get-H5BytesCell)
                # t216 A1: a BARE name is an identity row ONLY when the row DECLARES a sha12. A prose row that
                # merely mentions a file name (EVIDENCE_T102.md:29) must not enter the advisory at all -- its
                # `| 2 |` index cell used to be read as bytes=2 and the old advice ("write the full path") then
                # turned that prose row into a hard HASH-MISMATCH.
                if ($null -ne $bs) {
                    $bshown = [string]$bs
                    if ($bshown -eq '') { $bshown = '<none>' }
                    [void]$h5Bare.Add(@{ Doc = $docRel; Line = $i + 1; Name = $bm.Groups[1].Value; Sha = $bshown })
                }
            }
            continue
        }
        $name = $Matches[1]
        $cells = @($line.Split('|') | ForEach-Object { $_.Trim() })
        # accept BOTH '12345' and '12345 / 78' (bytes / lines) -- the t31 evidence tables use the latter,
        # and the old pattern silently skipped the byte cross-check for every such row
        $shaCell = ($cells | Where-Object { $_ -match '^`?[0-9A-Fa-f]{12}`?$' } | Select-Object -First 1)
        $bytesCell = Get-H5BytesCell $cells $shaCell   # t238 A: never the first logical column (index / label)
        if ($null -eq $bytesCell -and $null -eq $shaCell) { continue }   # not an identity row
        $h5Rows++
        # resolve the file: doc dir, doc dir/evidence, central dir, evidence index
        $cands = @(($docDir + '/' + $name), ($docDir + '/evidence/' + $name), ($EvidenceDir.TrimEnd('/') + '/' + $name))
        if ($evidenceIndex.ContainsKey($name)) { $cands += $evidenceIndex[$name] }
        $resolved = ''
        foreach ($c in $cands) { if (Test-Path -LiteralPath $c) { $resolved = $c; break } }
        if ($resolved -eq '') {
            [void]$h5Absent.Add(@{ Doc = $docRel; Line = $i + 1; Name = $name })
            continue
        }
        $actualBytes = (Get-Item -LiteralPath $resolved).Length
        $actualSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolved).Hash.Substring(0, 12).ToUpperInvariant()
        $expBytes = -1          # -1 = "bytes NOT DECLARED" (printed as <not-declared>, never compared)
        if ($null -ne $bytesCell) {
            # the cell may be '12345' or '12345 / 78' (bytes / lines) -- take the FIRST number only
            $mb = [regex]::Match($bytesCell, '([0-9][0-9,]*)')
            if ($mb.Success) { $expBytes = [int64]($mb.Groups[1].Value -replace ',', '') }
        }
        $expSha = ''
        if ($null -ne $shaCell) { $expSha = ($shaCell -replace '`', '').ToUpperInvariant() }
        if ($null -ne $shaCell -and $shaCell -cmatch '[a-f]') { [void]$h5Lower.Add(@{ Doc = $docRel; Line = $i + 1; Sha = $shaCell }) }
        $bad = $false
        if ($expBytes -ge 0 -and $expBytes -ne $actualBytes) { $bad = $true }
        if ($expSha -ne '' -and $expSha -ne $actualSha) { $bad = $true }
        if ($bad) {
            [void]$h5Mismatch.Add(@{ Doc = $docRel; Line = $i + 1; Path = $resolved; ExpBytes = $expBytes; ActBytes = $actualBytes; ExpSha = $expSha; ActSha = $actualSha })
        }
    }
}
if ($h5Mismatch.Count -eq 0) {
    Add-Check 'H5-evidence-identity' 'PASS' ("scannedRows=" + $h5Rows + " mismatch=0 absent=" + $h5Absent.Count + " bareRows=" + $h5Bare.Count) 'every documented bytes/sha12 pair matches the file on disk' 'identity must be checked per row against the hash -- "exists" is not "is the file it claims to be"'
}
else {
    Add-Check 'H5-evidence-identity' 'FAIL' ("scannedRows=" + $h5Rows + " mismatch=" + $h5Mismatch.Count + " absent=" + $h5Absent.Count + " bareRows=" + $h5Bare.Count) 'every documented bytes/sha12 pair matches the file on disk' 'a document that states a stale byte count/hash turns "verified" into unverifiable (the numbers look plausible on the review page)'
}
foreach ($m in $h5Mismatch) {
    $eb = [string]$m.ExpBytes
    if ($m.ExpBytes -lt 0) { $eb = '<not-declared>' }
    $es = [string]$m.ExpSha
    if ($es -eq '') { $es = '<not-declared>' }
    Write-Output ("FINDING|H5|HASH-MISMATCH|" + $m.Doc + ":" + $m.Line + "|" + $m.Path + "|expectedBytes=" + $eb + " actualBytes=" + $m.ActBytes + " expectedSha12=" + $es + " actualSha12=" + $m.ActSha + "|note=whichever side says <not-declared> was not compared")
}
foreach ($a in $h5Absent) {
    Write-Output ("NOTE|H5|file-absent|" + $a.Doc + ":" + $a.Line + "|" + $a.Name + "|advice=H2b owns the missing-file judgement; nothing fails here")
}
foreach ($lw in $h5Lower) {
    Write-Output ("NOTE|H5|lowercase-sha12|" + $lw.Doc + ":" + $lw.Line + "|" + $lw.Sha + "|advice=the project states sha12 UPPERCASE; value compared case-insensitively here")
}
foreach ($b in $h5Bare) {
    Write-Output ("NOTE|H5|bare-name-identity-row|" + $b.Doc + ":" + $b.Line + "|" + $b.Name + "|declaredSha12=" + $b.Sha + "|advice=this identity row uses a BARE filename, so H5 cannot compare it against the bytes on disk. FIX (t216 A3): write the FULL path AND update the bytes/sha12 to the values measured NOW; if the row is a HISTORICAL round reading, DELETE its numbers and label it as that round's reading instead -- renaming alone can produce a hard HASH-MISMATCH (measured 2026-09-12)")
}
if ($h5Bare.Count -gt 0) {
    Write-Output ("NOTE|H5|bare-name-identity-rows|count=" + $h5Bare.Count + "|advice=advisory only, nothing fails here: these rows declare a sha12 but write a bare filename, so they sit outside the H5 scan face (same family as the t31 row that stated C8AB14D320B9 while the file was C565252A8DBD). Rewrite them with the full path AND current numbers, or drop the numbers if the row is a historical reading")
}

# ---------------------------------------------------------------------------------------------
# ---------------------------------------------------------------------------------------------
# H8: plugin-directory two-copy probe (t295, verifier 2026-09-13). INFO ONLY -- no check, no verdict impact.
# WHY: a plugin directory that exists in TWO places silently forks behaviour and the reader cannot tell
# which copy is the live one. The check must SEARCH BY FACE, never by a hard-coded guessed path: naming a
# path nobody measured is how a gate starts asserting something about a directory that may not exist.
# Output per hit: absolute path + sha256_12 of its manifest when present; a missing copy is written `absent`.
# RULE: when nothing matches, the correct reading is `result=absent` -- a reading, not a failure.
$h8Dirs = @()
foreach ($h8Root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $h8Root)) { continue }
    $h8Dirs += @(Get-ChildItem -Recurse -Directory -Depth 3 -Path $h8Root -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^(?i:plugins?)$' -and $_.FullName -notmatch '\\(bin|obj|reversed|\.git)\\' })
}
$h8Dirs = @($h8Dirs | Sort-Object -Property FullName -Unique)
if ($h8Dirs.Count -eq 0) {
    Write-Output ("NOTE|H8|plugin-dir-copies|result=absent|searched=" + ($scanRoots -join ',') + "|depth=3|nameMatch=plugins?|advice=no plugin directory inside the scanned roots: there is no second copy to compare. A caller that means a directory OUTSIDE the repo must say so explicitly and give two absolute paths plus each sha256_12")
} else {
    foreach ($h8d in $h8Dirs) {
        Write-Output ("NOTE|H8|plugin-dir-copies|path=" + $h8d.FullName + "|kind=in-repo|secondCopy=absent|advice=name the second copy explicitly when one exists outside the repo (two absolute paths + each sha256_12); a single in-repo copy is INFO, never a FAIL")
    }
}
# ---------------------------------------------------------------------------------------------
# H9: same-line `)` + `Write-Output` adjacency -- REGRESSION READOUT (t295, verifier 2026-09-13). INFO ONLY.
# HISTORY: this shape is the `:943` defect of the pre-e5a77ad generation (measured 1); after the fix landed at
# 2026-09-12 19:39:56.623 it is 0, and it has been 0 on every generation measured since.
# PREDICATE (verbatim, whole-text, NOT line-based): ) then horizontal-whitespace only then Write-Output.
# [!] DO NOT promote the CROSS-LINE sibling to an acceptance criterion: it DRIFTS with each generation
# (e5a77ad = 3, measured 4 later) because a legal line-wrapped statement is indistinguishable from it.
# A criterion whose value drifts per generation is a false-red generator; it may be reported, never gated.
# Reading it line-by-line is also useless: Select-String reads a line at a time, so a cross-line pattern
# can only ever return 0 there -- use whole-text ReadAllText + [regex]::Matches for both counters.
$h9Self = $PSCommandPath
if ($h9Self -and (Test-Path -LiteralPath $h9Self)) {
    $h9Txt = [IO.File]::ReadAllText($h9Self)
    $h9Same = ([regex]::Matches($h9Txt, '\)[^\S\r\n]*Write-Output[^\S\r\n]')).Count
    $h9Cross = ([regex]::Matches($h9Txt, '\)\s*Write-Output\s')).Count
    Write-Output ("NOTE|H9|noncrossline-writeout|self=" + $h9Self + "|sameLineCount=" + $h9Same + "|crossLineCount=" + $h9Cross + "|regressionItem=sameLineCount must stay 0|advice=crossLineCount is INFORMATIONAL ONLY (it drifts with each generation) and must never be used as an acceptance criterion")
}
# H7: astral emoji / U+FE0F in UI sources (captain 2026-09-12; scanner body contributed by ui2).
# WHY: emoji render inconsistently (font/colour) and U+FE0F is invisible in review, so neither belongs in
# user-visible copy; the existing stock is a RATCHET (a measured baseline that may only decrease).
# THREE criteria -- missing any one gives false negatives or false positives:
#   1. astral characters must be decoded as SURROGATE PAIRS: match [\uD800-\uDBFF][\uDC00-\uDFFF] and then
#      ConvertToUtf32. Reading UTF-16 code units one by one sees U+1F534 as two BMP chars and misses EVERY
#      astral emoji.
#   2. U+FE0F gets its OWN column: U+26A0 alone is a text symbol, U+26A0 U+FE0F renders as a colour emoji.
#      Keeping them separate is what lets a reader tell "symbol" from "emoji".
#   3. per-owner attribution (byOwner=) plus a baseline a caller can LOWER to force a FAIL (reverse control).
# This file must stay ASCII-only (it does). Measured trap: a scanner passed through
# `powershell -Command "...non-ASCII..."` gets its arguments decoded as ANSI on PS 5.1 and then reports hits in
# CLEAN files -- which is why the pattern is written as regex \uXXXX escapes and nothing here is literal.
# ---------------------------------------------------------------------------------------------
$emojiAstralRe = '[\uD800-\uDBFF][\uDC00-\uDFFF]'
$emojiFe0fRe = '\uFE0F'
# t258: every skip in the H7 scan is COUNTED and NAMED. `catch { continue }` made an unreadable file look like
# a clean file ("this file has no emoji") when the truth was "this file was never read". The two are not the
# same claim, and the silent form cannot be audited after the fact.
$emSkipped = New-Object System.Collections.ArrayList
$emExt = @('.cs', '.xaml')
$emFiles = @(Get-ChildItem -Path $EmojiScope -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and ($emExt -contains $_.Extension.ToLower()) })
$emAstral = 0; $emFe0f = 0; $emLines = 0; $emFilesHit = 0
$emByOwner = @{}
foreach ($emF in $emFiles) {
    $emTxt = ''
    try { $emTxt = [IO.File]::ReadAllText($emF.FullName) }
    catch {
        [void]$emSkipped.Add(@{ Stage = 'worktree-read'; Rel = (Get-RelPath $emF.FullName); Reason = $_.Exception.GetType().Name })
        continue
    }
    $emA = 0
    foreach ($emM in [regex]::Matches($emTxt, $emojiAstralRe)) {
        $emCp = [char]::ConvertToUtf32($emM.Value, 0)
        if ($emCp -ge 0x1F000 -and $emCp -le 0x1FAFF) { $emA++ }
    }
    $emC = ([regex]::Matches($emTxt, $emojiFe0fRe)).Count
    if ($emA -gt 0 -or $emC -gt 0) {
        $emAstral += $emA
        $emFe0f += $emC
        $emFilesHit++
        foreach ($emRow in ($emTxt -split "`n")) {
            $emHit = $false
            foreach ($emM in [regex]::Matches($emRow, $emojiAstralRe)) {
                $emCp = [char]::ConvertToUtf32($emM.Value, 0)
                if ($emCp -ge 0x1F000 -and $emCp -le 0x1FAFF) { $emHit = $true; break }
            }
            if (-not $emHit -and $emRow -match $emojiFe0fRe) { $emHit = $true }
            if ($emHit) { $emLines++ }
        }
        $emRel = Get-RelPath $emF.FullName
        $emOwn = 'UNMAPPED'
        foreach ($emMap in $ownerMap) { if ($emRel -match $emMap.Rx) { $emOwn = $emMap.Owner; break } }
        if (-not $emByOwner.ContainsKey($emOwn)) { $emByOwner[$emOwn] = @{ F = 0; A = 0; C = 0 } }
        $emByOwner[$emOwn].F = $emByOwner[$emOwn].F + 1
        $emByOwner[$emOwn].A = $emByOwner[$emOwn].A + $emA
        $emByOwner[$emOwn].C = $emByOwner[$emOwn].C + $emC
        Write-Output ("STOCK|H7|EMOJI|" + $emRel + "|astral=" + $emA + " fe0f=" + $emC + "|owner=" + $emOwn)
    }
}
# t216 (captain 2026-09-12): the 53/51 baselines were MEASURED on a worktree that carried in-flight
# untracked files, so a worktree stock is not reproducible from HEAD and an in-flight emoji turned the
# ratchet red before it was ever committed. DEFAULT: the compared stock is the COMMITTED stock (HEAD),
# so "NEW" only counts committed increments; the worktree stock keeps its own two keys for comparison.
# Fallback: when -EmojiScope is not a repo-relative path (synthetic counter-control corpora), there is no
# HEAD to read, so the worktree stock is compared instead and stockSource says so.
$emStockA = $emAstral
$emStockC = $emFe0f
$emStockSrc = 'worktree'
$emHeadMembers = 0
$emHeadReadFail = 0
$emHeadReadOk = 0
$emScopeRel = $EmojiScope.Replace('\', '/').TrimEnd('/')
if ($emScopeRel -ne '' -and $emScopeRel -notmatch '^([A-Za-z]:|/|\.\.)') {
    # Deterministic HEAD-side count: git ls-tree for membership, then git cat-file blob per member.
    # t312: a FAILED `git cat-file` still creates a 0-byte temp file through `>`, and ReadAllText() returns
    # "" WITHOUT throwing -- the old code therefore counted that member as "no emoji" (silent undercount ->
    # newAstral = Max(0, undercount - baseline) = 0 -> false PASS, with git''s own error discarded by 2>nul).
    # Now every member read is verified (cmd exit code + temp-file length vs the blob size) and any
    # unverifiable read is COUNTED, NAMED, and forces the stock to fall back to the worktree + the check to
    # INCONCLUSIVE. The loop is wrapped in try/finally so an interrupted run cannot leave the temp file behind.
    # NO text pipeline (a native stdout pipe would be decoded with the console code page and could mangle
    # astral characters); the blob is redirected to a temp file byte-for-byte -- same discipline as the
    # evidence captures. Every failure path FALLS BACK to the worktree stock and SAYS SO: a silent 0 here
    # would turn this ratchet into an always-PASS check (caught by the -EmojiAstralBaseline reverse control).
    $emHeadList = @(& git ls-tree -r HEAD --name-only -- $emScopeRel 2>$null)
    if ($emHeadList.Count -eq 0) {
        $emStockSrc = 'worktree(reason=no-head-list)'
    }
    else {
        $emHa = 0; $emHc = 0
        # t258: per-RUN temp path. `gate-h7-head.tmp` was a fixed name shared by every concurrent run in this
        # repo (teammates do run the gate in the same window), so one run could read another run's blob.
        # t312: try/finally does NOT run when the process is HARD-KILLED (a terminated process executes no
        # finally), so an interrupted run can still leave its blob copy in %TEMP%. Sweep only PROVABLY stale
        # ones: same pattern, the run''s pid is no longer alive, and the file is older than 10 minutes. A LIVE
        # concurrent run is never touched (its pid is alive), so this can never make a peer''s read fail.
        $emSwept = 0
        foreach ($emStale in @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter 'gate-h7-head-*.tmp' -File -ErrorAction SilentlyContinue)) {
            if (((Get-Date) - $emStale.LastWriteTime).TotalMinutes -lt 10) { continue }
            if ($emStale.Name -match 'gate-h7-head-(\d+)-') {
                $emOldPid = [int]$Matches[1]
                if (@(Get-Process -Id $emOldPid -ErrorAction SilentlyContinue).Count -gt 0) { continue }
            }
            Remove-Item -LiteralPath $emStale.FullName -Force -ErrorAction SilentlyContinue
            $emSwept++
        }
        if ($emSwept -gt 0) { Write-Output ("NOTE|H7|stale-temp-swept|count=" + $emSwept + "|runId=" + $gateRunId + "|advice=leftovers of hard-killed runs (a terminated process runs no finally). Only files older than 10 minutes whose pid is gone are removed; a live concurrent run is never touched") }
        $emTmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), ('gate-h7-head-' + $gateRunId + '.tmp'))
        Write-Output ("H7-TEMP|path=" + $emTmp + "|pid=" + $gatePid + "|runId=" + $gateRunId + "|unique=per-process")
        try {
        foreach ($emF2 in $emFiles) {
            $emRel2 = Get-RelPath $emF2.FullName
            if ($emHeadList -notcontains $emRel2) { continue }
            $emHeadMembers++
            cmd /c "git cat-file blob `"HEAD:$emRel2`" > `"$emTmp`" 2>nul"
            $emRc2 = $LASTEXITCODE
            if ($emRc2 -ne 0) {
                $emHeadReadFail++
                [void]$emSkipped.Add(@{ Stage = 'head-blob-read'; Rel = $emRel2; Reason = ('git-cat-file rc=' + $emRc2) })
                continue
            }
            $emT2 = ''
            try { $emT2 = [IO.File]::ReadAllText($emTmp, [Text.Encoding]::UTF8) }
            catch {
                [void]$emSkipped.Add(@{ Stage = 'head-blob-read'; Rel = $emRel2; Reason = $_.Exception.GetType().Name })
                continue
            }
            $emLen2 = -1
            try { $emLen2 = (Get-Item -LiteralPath $emTmp -ErrorAction Stop).Length } catch { $emLen2 = -1 }
            if ($emLen2 -lt 0) {
                $emHeadReadFail++
                [void]$emSkipped.Add(@{ Stage = 'head-blob-read-incomplete'; Rel = $emRel2; Reason = 'temp-file-missing' })
                continue
            }
            if ($emLen2 -eq 0) {
                $emBlobSize2 = -1
                try { $emBlobSize2 = [int](& git cat-file -s "HEAD:$emRel2" 2>$null) } catch { $emBlobSize2 = -1 }
                if ($emBlobSize2 -ne 0) {
                    $emHeadReadFail++
                    [void]$emSkipped.Add(@{ Stage = 'head-blob-read-incomplete'; Rel = $emRel2; Reason = ('readBytes=0 blobSize=' + $emBlobSize2) })
                    continue
                }
            }
            $emHeadReadOk++
            foreach ($emM2 in [regex]::Matches($emT2, $emojiAstralRe)) {
                $emCp2 = [char]::ConvertToUtf32($emM2.Value, 0)
                if ($emCp2 -ge 0x1F000 -and $emCp2 -le 0x1FAFF) { $emHa++ }
            }
            $emHc += ([regex]::Matches($emT2, $emojiFe0fRe)).Count
        }
        }
        finally { Remove-Item -LiteralPath $emTmp -Force -ErrorAction SilentlyContinue }
        if ($emHeadMembers -gt 0 -and $emHeadReadFail -eq 0) { $emStockA = $emHa; $emStockC = $emHc; $emStockSrc = 'committed(HEAD)' }
        elseif ($emHeadMembers -gt 0) { $emStockSrc = ('worktree(reason=head-read-incomplete:' + $emHeadReadFail + '/' + $emHeadMembers + ')') }
        else { $emStockSrc = 'worktree(reason=no-head-members)' }
    }
}
$emNewA = [Math]::Max(0, $emStockA - $EmojiAstralBaseline)
$emNewC = [Math]::Max(0, $emStockC - $EmojiFe0fBaseline)
$emBy = (@($emByOwner.Keys | Sort-Object | ForEach-Object { $_ + '=' + $emByOwner[$_].F + 'f/' + $emByOwner[$_].A + '+' + $emByOwner[$_].C })) -join ','
$emMeasured = ("scope=" + $EmojiScope + "|ext=.cs,.xaml|scannedFiles=" + $emFiles.Count + "|skipped=" + $emSkipped.Count + "|emojiAstral=" + $emAstral + "|fe0f=" + $emFe0f + "|hitLines=" + $emLines + "|filesHit=" + $emFilesHit + "|baselineAstral=" + $EmojiAstralBaseline + "|baselineFe0f=" + $EmojiFe0fBaseline + "|baselineAt=" + $EmojiBaselineAt + "|stockSource=" + $emStockSrc + "|headMembers=" + $emHeadMembers + "|headReadOk=" + $emHeadReadOk + "|headReadFail=" + $emHeadReadFail + "|stockAstral=" + $emStockA + "|stockFe0f=" + $emStockC + "|newAstral=" + $emNewA + "|newFe0f=" + $emNewC + "|byOwner=" + $emBy)
if ($emHeadReadFail -gt 0) {
    Add-Check 'H7-emoji-in-ui-copy' 'INCONCLUSIVE' $emMeasured 'no NEW astral emoji (U+1F000..U+1FAFF) and no NEW U+FE0F in UI sources' 't312: at least one HEAD-side blob read could not be verified (see headReadFail= / NOTE|H7|head-read-incomplete), so the committed stock is not trustworthy for this run and the ratchet cannot be judged. NOTE the OLD hazard: a failed `git cat-file` still creates a 0-byte file via `>`, and ReadAllText returns "" WITHOUT throwing, so this member used to be counted as "no emoji" (silent undercount -> newAstral=Max(0,undercount-baseline)=0 -> false PASS)'
}
elseif ($emNewA -eq 0 -and $emNewC -eq 0) {
    Add-Check 'H7-emoji-in-ui-copy' 'PASS' $emMeasured 'no NEW astral emoji (U+1F000..U+1FAFF) and no NEW U+FE0F in UI sources' 'RATCHET: the stock is a measured baseline and may only decrease. Astral detection must decode surrogate pairs (1) and U+FE0F is tracked in its own column (2); byOwner shows whose territory the remaining stock sits in (3). Lower -EmojiAstralBaseline / -EmojiFe0fBaseline to force a FAIL (reverse control). t216: the stock COMPARED is the COMMITTED stock by default -- see stockSource / baselineAt / stockAstral / stockFe0f (the emojiAstral / fe0f keys keep reporting the worktree stock), so an in-flight untracked file can no longer turn the ratchet red before it is committed; a non-repo -EmojiScope has no HEAD and falls back to the worktree stock with stockSource=worktree'
}
else {
    Add-Check 'H7-emoji-in-ui-copy' 'FAIL' $emMeasured 'no NEW astral emoji (U+1F000..U+1FAFF) and no NEW U+FE0F in UI sources' 'a NEW emoji means either UI copy gained one or a comment gained one: user-visible copy must be ASCII, and comment severity markers use the in-repo ASCII convention (e.g. [!] instead of U+1F534 or U+26A0 U+FE0F). Check byOwner to see whose files moved. t216: what fires is a COMMITTED stock above the baseline; an in-flight emoji still shows in the emojiAstral / fe0f columns but does not enter newAstral / newFe0f until it is committed'
}

# t258: skips are ANNOUNCED, never swallowed. A counted warning lets a reader decide whether the H7 reading is
# usable ("all 76 files read" vs "3 unreadable, stock may be undercounted") instead of having to trust it.
if ($emHeadReadFail -gt 0) {
    Write-Output ("NOTE|H7|head-read-incomplete|count=" + $emHeadReadFail + "|ofMembers=" + $emHeadMembers + "|runId=" + $gateRunId + "|advice=the compared H7 stock is NOT the committed stock for this run: at least one HEAD-side blob read could not be verified, so stockSource fell back to the worktree. Do not read the H7 row as clean and do not cite this window without saying so")
}
if (($emHeadReadOk + $emHeadReadFail) -ne $emHeadMembers) {
    Write-Output ("NOTE|H7|head-read-accounting|headReadOk=" + $emHeadReadOk + "|headReadFail=" + $emHeadReadFail + "|headMembers=" + $emHeadMembers + "|runId=" + $gateRunId + "|advice=the two read outcomes must sum to headMembers; a mismatch means a member was neither read nor counted")
}
if ($emSkipped.Count -gt 0) {
    foreach ($emSk in $emSkipped) {
        Write-Output ("NOTE|H7|file-skipped|" + $emSk.Rel + "|stage=" + $emSk.Stage + "|reason=" + $emSk.Reason + "|runId=" + $gateRunId + "|advice=this file was NOT counted in the H7 stock: treat the stock as a LOWER BOUND for it, do not read the row as 'clean'")
    }
    Write-Output ("NOTE|H7|files-skipped|count=" + $emSkipped.Count + "|ofScanned=" + $emFiles.Count + "|runId=" + $gateRunId + "|advice=the H7 stock excludes every file above; the measured= field carries the same number as skipped=")
}

foreach ($r in $results) {
    Write-Output ("CHECK|" + $r.id + "|verdict=" + $r.verdict + "|measured=" + $r.measured + "|expected=" + $r.expected + "|note=" + $r.note)
}
$fail = @($results | Where-Object { $_.verdict -eq 'FAIL' }).Count
$pass = @($results | Where-Object { $_.verdict -eq 'PASS' }).Count
$inc = @($results | Where-Object { $_.verdict -eq 'INCONCLUSIVE' }).Count
$overall = 'PASS'
$exit = 0
if ($fail -gt 0) { $overall = 'FAIL'; $exit = 1 }
elseif ($inc -gt 0) { $overall = 'INCONCLUSIVE'; $exit = 0 }
# SELF-CHECK (captain 2026-09-12): the accounting line is produced BY THIS TOOL, immediately before SUMMARY, and
# carries the SOURCES of both sides so a reader can recompute it. Two independently-derived numbers:
#   checks = number of registered checks ($results.Count)
#   sum    = pass + fail + inconclusive, each counted from the verdict field
#   other  = checks - sum  (a verdict string that is none of the three -- the drift this line exists to catch)
# equal=True iff other == 0. Falsifiable: give any check a verdict outside the three and equal turns False.
$ackChecks = $results.Count
$ackSum = $pass + $fail + $inc
$ackOther = $ackChecks - $ackSum
# ---------------------------------------------------------------------------------------------
# H10: delivery-face (dist/) visibility -- INFO ONLY (captain 2026-09-13). NOTE, never a CHECK:
# it does not enter checks= and cannot change the verdict.
# WHY: .gitignore ignores `dist/`, so the ENTIRE delivery tree -- the published build, every rotated
# backup tree, and any runtime artefact the delivered binary writes next to itself -- is OUTSIDE
# `git status --porcelain` and OUTSIDE H1..H7. MEASURED 2026-09-13: a delivered build had written its own
# shell-startup.log into the tree that was later rotated to dist\AIPlayer.bak-204736, and the rotation
# itself is silent too. Consequence: "the delivery face is clean" has NO gate backing -- it can only be a
# hand-written four-piece carrying a sampling moment.
$h10Dist = Join-Path $repoRoot 'dist'
if (-not (Test-Path -LiteralPath $h10Dist)) {
    Write-Output ("NOTE|H10-dist-visibility|result=absent|advice=no dist/ directory at the repo root in this run")
}
else {
    $h10Trees = New-Object System.Collections.ArrayList
    $h10Live = ''
    $h10LiveStray = 'absent'
    foreach ($h10D in @(Get-ChildItem -LiteralPath $h10Dist -Directory -ErrorAction SilentlyContinue | Sort-Object -Property Name)) {
        $h10N = @(Get-ChildItem -LiteralPath $h10D.FullName -Recurse -File -ErrorAction SilentlyContinue).Count
        $h10Stray = 0
        if (Test-Path -LiteralPath (Join-Path $h10D.FullName 'shell-startup.log')) { $h10Stray = 1 }
        [void]$h10Trees.Add($h10D.Name + ':' + $h10N + ',created=' + $h10D.CreationTime.ToString('MM-dd HH:mm:ss') + ',stray=' + $h10Stray)
        if ($h10D.Name -eq 'AIPlayer') {
            if ($h10Stray -eq 1) {
                $h10P = Join-Path $h10D.FullName 'shell-startup.log'
                $h10LiveStray = 'present bytes=' + (Get-Item -LiteralPath $h10P).Length + ' sha256_12=' + ((Get-FileHash -Algorithm SHA256 -LiteralPath $h10P).Hash.Substring(0,12))
            }
            foreach ($h10F in @('AIPlayer.Shell.exe','AIPlayer.Shell.dll','player\AIPlayer.MpvHost.dll','player\libmpv-2.dll')) {
                $h10FP = Join-Path $h10D.FullName $h10F
                if (Test-Path -LiteralPath $h10FP) {
                    $h10Live += ((Split-Path -Leaf $h10F) + '=' + (Get-Item -LiteralPath $h10FP).Length + '/' + ((Get-FileHash -Algorithm SHA256 -LiteralPath $h10FP).Hash.Substring(0,12)) + ' ')
                }
                else { $h10Live += ((Split-Path -Leaf $h10F) + '=ABSENT ') }
            }
        }
    }
    Write-Output ("NOTE|H10-dist-visibility|trees=" + $h10Trees.Count + "[" + ($h10Trees -join '][') + "]|liveFour=" + $h10Live.Trim() + "|liveRunTimeLog=" + $h10LiveStray + "|advice=the delivery face is OUTSIDE porcelain and H1-H7 (dist/ is gitignored): never state it is clean without a hand-sampled four-piece = sampled moment + tree list (with creation time) + file counts + whether it is a published generation. INFO only, no verdict impact")
}
Write-Output ("SELF-CHECK|checks=" + $ackChecks + "|pass=" + $pass + "|fail=" + $fail + "|inconclusive=" + $inc + "|sum=" + $ackSum + "|other=" + $ackOther + "|equal=" + ($ackOther -eq 0) + "|source=checks=count(results);sum=count(PASS)+count(FAIL)+count(INCONCLUSIVE);other=checks-sum|at=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))
Write-Output ("SUMMARY|verdict=" + $overall + "|checks=" + $results.Count + "|pass=" + $pass + "|fail=" + $fail + "|inconclusive=" + $inc + "|scannedFiles=" + $scanned + "|citedNames=" + $cited.Count + "|pid=" + $gatePid + "|runId=" + $gateRunId)

if ($Json -ne '') {
    $obj = @{
        sampled = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
        scanRoot = $ScanRoot; extraScanPath = $ExtraScanPath; evidenceDir = $EvidenceDir
        scannedFiles = $scanned; citedNames = $cited.Count
        verdict = $overall; pass = $pass; fail = $fail
        checks = $results
        privacyFindings = $privacyHits
        untrackedCited = $untrackedCited
        missingCited = $missingCited
        deletedInWorktree = $deletedInWorktree
    }
    [System.IO.File]::WriteAllText($Json, ($obj | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("JSON|path=" + $Json)
}
exit $exit
