<#
    dist-audit.ps1 - release-face audit for an AIPlayer shell dist tree.

    ONE command: given a dist root and the expected kernel fork sha256_12, print
      (1) the four named objects (shell exe / shell dll / shell services dll / kernel fork dll)
          with bytes + sha12 + mtime,
      (2) the DECOY standing: a top-level AIPlayer.MpvHost.dll must never be credited as a pass
          (pass credit comes only from the sha12 match, never from file presence),
      (3) three topology counts printed SEPARATELY: top-level files / recursive files / player\ recursive files,
      (4) the kernel trio presence (dll + libmpv-2.dll + .pri), scoped to player\ for release trees
          and to the top level for kernel-stage trees; every missing item is named,
      (5) build-face hygiene: zero-byte *.g.cs under the project obj dir, XamlSaveStateFile.xml presence,
          Assets parity against the source Assets tree (static counterpart of the runtime
          'SERVERS icon-missing' log line - a DIFFERENT instrument, see README),
      (6) exactly one 'SUMMARY|verdict=' line, and an exit code (0 = PASS, 1 = FAIL).

    READ-ONLY: this tool never writes inside the dist tree. Fixtures go to %TEMP%.

    ASCII-ONLY BY CONTRACT: the script body is pure ASCII (proved by -SelfCheck). The two Chinese
    labels the task asks for are built from code points so the file stays ASCII; their human text
    is spelled out in ASCII words in the comments right above each definition.
#>
[CmdletBinding()]
param(
    # dist (or kernel-stage) tree root to audit
    [string]$Dist = '',
    # expected sha256 first-12 of the kernel fork dll, e.g. 29469DC45F1D (sample value; replace per generation)
    [string]$KernelForkSha12 = '',
    # optional sha256 first-12 of the ORIGINAL stock kernel dll; lets the decoy line name its role
    # (original-stock vs other-generation) instead of only saying "not the expected fork"
    [string]$OriginalStockSha12 = '',
    # project obj dirs that are GATED for zero-byte *.g.cs; default = this repo's shell/App/obj
    [string[]]$ProjectObjDir = @(),
    # source Assets tree used for the dist/source parity count; default = this repo's shell/App/Assets
    [string]$SourceAssetsDir = '',
    # 0-byte *.g.cs budget (0 = FAIL on any occurrence)
    [int]$MaxZeroByteGcs = 0,
    # settle time between the two build-face samples (0 disables the second sample)
    [int]$SettleMs = 2000,
    # prove the ASCII-only + parse-clean contract of this file and exit
    [switch]$SelfCheck
)

$ErrorActionPreference = 'Stop'

$scriptRoot = if ($PSCommandPath) { Split-Path -Parent $PSCommandPath } else { $PSScriptRoot }

# Chinese label 1: code points 8BF1 9975 FF0C 4E0D 5F97 88AB 52A0 8F7D
#    human text (ASCII words): "decoy <fullwidth-comma> must not be loaded"
$LabelDecoy = [string]::Concat([char]0x8BF1, [char]0x9975, [char]0xFF0C, [char]0x4E0D, [char]0x5F97, [char]0x88AB, [char]0x52A0, [char]0x8F7D)
# Chinese label 2: code points 7F3A 5931
#    human text (ASCII words): "<fork> missing"
$LabelForkMissing = 'fork ' + [string]::Concat([char]0x7F3A, [char]0x5931)

function Get-FileSha12 {
    param([string]$FilePath)
    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.Substring(0, 12).ToUpperInvariant()
}

function Measure-FilesRecursive {
    param([string]$DirPath)
    if (-not (Test-Path -LiteralPath $DirPath)) { return 0 }
    return [IO.Directory]::GetFiles($DirPath, '*', [IO.SearchOption]::AllDirectories).Count
}

function Get-GeneratedSourceReading {
    # One sample of the generated-source face: totals, 0-byte set and the newest mtime seen.
    # A reading without a sampling time cannot be told apart from a stale one, so the time travels
    # with the numbers (same discipline the evidence gate applies to its own ratchet checks).
    param([string]$ObjDir)
    $allGenerated = @(Get-ChildItem -LiteralPath $ObjDir -Recurse -Filter '*.g.cs' -File -ErrorAction SilentlyContinue)
    $zeroGenerated = @($allGenerated | Where-Object { $_.Length -eq 0 })
    $newestGenerated = if ($allGenerated.Count -gt 0) { ($allGenerated | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') } else { '(none)' }
    return @{
        Total       = $allGenerated.Count
        Zero        = $zeroGenerated.Count
        ZeroPaths   = @($zeroGenerated | ForEach-Object { $_.FullName })
        NewestMtime = $newestGenerated
    }
}

# ---------------------------------------------------------------- -SelfCheck
# The contract is "the body is pure ASCII and parses". Both are checked against this very file,
# so the proof travels with the tool instead of living in a prose claim somewhere else.
if ($SelfCheck) {
    $selfBytes = [IO.File]::ReadAllBytes($PSCommandPath)
    $selfNonAscii = 0
    foreach ($selfByte in $selfBytes) { if ($selfByte -gt 127) { $selfNonAscii++ } }
    $selfTokens = $null
    $selfErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$selfTokens, [ref]$selfErrors)
    $selfVerdict = if ($selfNonAscii -eq 0 -and $selfErrors.Count -eq 0) { 'PASS' } else { 'FAIL' }
    Write-Output ("SELFCHECK|file=" + $PSCommandPath + "|bytes=" + $selfBytes.Length + "|nonAscii=" + $selfNonAscii + "|parseErrors=" + $selfErrors.Count + "|verdict=" + $selfVerdict)
    if ($selfVerdict -eq 'PASS') { exit 0 } else { exit 1 }
}

# ---------------------------------------------------------------- argument gate
$failReasons = New-Object System.Collections.ArrayList
$startedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')

if ([string]::IsNullOrWhiteSpace($Dist)) {
    Write-Output ("FINDING|ARG|missing-dist|note=-Dist is required (dist or kernel-stage tree root)")
    Write-Output ("SUMMARY|verdict=FAIL|started=" + $startedAt + "|fail=1|reason=missing-dist-argument")
    exit 1
}
if (-not (Test-Path -LiteralPath $Dist)) {
    Write-Output ("FINDING|ARG|dist-absent|path=" + $Dist)
    Write-Output ("SUMMARY|verdict=FAIL|started=" + $startedAt + "|fail=1|reason=dist-absent")
    exit 1
}
$distRoot = (Get-Item -LiteralPath $Dist).FullName

$expectedFork = $KernelForkSha12.ToUpperInvariant()
if ($expectedFork -notmatch '^[0-9A-F]{12}$') {
    # A malformed expectation must never silently degrade into "nothing to compare" - that is the
    # classic empty reading. It is a hard FAIL that names the offending value.
    [void]$failReasons.Add('bad-fork-sha12-argument')
    Write-Output ("FINDING|ARG|bad-fork-sha12|value=" + $KernelForkSha12 + "|expected=12 hex chars")
}

if ($ProjectObjDir.Count -eq 0) {
    $ProjectObjDir = @([IO.Path]::GetFullPath((Join-Path $scriptRoot '..\App\obj')))
}
if ([string]::IsNullOrWhiteSpace($SourceAssetsDir)) {
    $SourceAssetsDir = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\App\Assets'))
}

Write-Output ("DIST-AUDIT|tool=dist-audit.ps1|started=" + $startedAt + "|dist=" + $distRoot + "|expectedForkSha12=" + $expectedFork + "|originalStockSha12=" + $(if ([string]::IsNullOrWhiteSpace($OriginalStockSha12)) { '(not-given)' } else { $OriginalStockSha12.ToUpperInvariant() }) + "|settleMs=" + $SettleMs + "|zeroByteGcsBudget=" + $MaxZeroByteGcs)

# ---------------------------------------------------------------- shape
# Three shapes are recognised, because a kernel-stage tree legitimately has NO shell objects and
# gating them there would report a false red. The shape is printed, never assumed.
$shellExePath = Join-Path $distRoot 'AIPlayer.Shell.exe'
$kernelExePath = Join-Path $distRoot 'AIPlayer.MpvHost.exe'
$playerDir = Join-Path $distRoot 'player'
$playerDirExists = Test-Path -LiteralPath $playerDir
$topKernelDllPath = Join-Path $distRoot 'AIPlayer.MpvHost.dll'

if (Test-Path -LiteralPath $shellExePath) {
    $shape = 'release'
} elseif (Test-Path -LiteralPath $kernelExePath) {
    $shape = 'kernel-stage'
} else {
    $shape = 'unknown'
}
$shapeEvidence = if (Test-Path -LiteralPath $shellExePath) { 'AIPlayer.Shell.exe present' }
    elseif (Test-Path -LiteralPath $kernelExePath) { 'AIPlayer.MpvHost.exe present, AIPlayer.Shell.exe absent' }
    else { 'neither AIPlayer.Shell.exe nor AIPlayer.MpvHost.exe present' }
Write-Output ("SHAPE|kind=" + $shape + "|evidence=" + $shapeEvidence)
if ($shape -eq 'unknown') { [void]$failReasons.Add('unknown-tree-shape') }

# ---------------------------------------------------------------- named objects
$shellObjectNames = @('AIPlayer.Shell.exe', 'AIPlayer.Shell.dll', 'AIPlayer.Shell.Services.dll')
$shellRoleByIndex = @('shell-exe', 'shell-dll', 'shell-services-dll')
for ($objectIndex = 0; $objectIndex -lt $shellObjectNames.Count; $objectIndex++) {
    $objectName = $shellObjectNames[$objectIndex]
    $objectRole = $shellRoleByIndex[$objectIndex]
    $objectPath = Join-Path $distRoot $objectName
    if (Test-Path -LiteralPath $objectPath) {
        $objectItem = Get-Item -LiteralPath $objectPath
        Write-Output ("OBJECT|role=" + $objectRole + "|path=" + $objectName + "|present=True|bytes=" + $objectItem.Length + "|sha12=" + (Get-FileSha12 $objectPath) + "|mtime=" + $objectItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))
        if ($objectItem.Length -eq 0) { [void]$failReasons.Add('zero-byte-object:' + $objectName) }
    } else {
        $applicability = if ($shape -eq 'release') { 'required' } else { 'not-applicable(' + $shape + ' tree)' }
        Write-Output ("OBJECT|role=" + $objectRole + "|path=" + $objectName + "|present=False|applicability=" + $applicability)
        if ($shape -eq 'release') { [void]$failReasons.Add('shell-object-absent:' + $objectName) }
    }
}

# ---------------------------------------------------------------- kernel fork resolution
# Resolution order: player\AIPlayer.MpvHost.dll (v4 release layout) wins when the dir exists;
# otherwise the top-level dll is the candidate (t130 kernel-stage layout). Whether the candidate
# IS the fork is decided by sha12 only - never by its name or its presence.
$forkPath = ''
$forkSource = ''
$playerKernelDllPath = Join-Path $playerDir 'AIPlayer.MpvHost.dll'
if ($playerDirExists -and (Test-Path -LiteralPath $playerKernelDllPath)) {
    $forkPath = $playerKernelDllPath
    $forkSource = 'player'
} elseif (Test-Path -LiteralPath $topKernelDllPath) {
    $forkPath = $topKernelDllPath
    $forkSource = 'top'
}

if ($forkPath -eq '') {
    [void]$failReasons.Add('fork-missing')
    Write-Output ("KERNEL-FORK|resolved=<none>|source=<none>|expected=" + $expectedFork + "|reason=" + $LabelForkMissing + "|note=no AIPlayer.MpvHost.dll found in " + $(if ($playerDirExists) { 'player\ (exists but empty) or top level' } else { 'player\ (absent) or top level' }))
} else {
    $forkActualSha12 = Get-FileSha12 $forkPath
    $forkItem = Get-Item -LiteralPath $forkPath
    $forkMatch = ($forkActualSha12 -eq $expectedFork)
    Write-Output ("OBJECT|role=kernel-fork|path=" + $forkPath.Substring($distRoot.Length).TrimStart('\') + "|present=True|bytes=" + $forkItem.Length + "|sha12=" + $forkActualSha12 + "|mtime=" + $forkItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))
    if ($forkMatch) {
        Write-Output ("KERNEL-FORK|resolved=" + $forkPath + "|source=" + $forkSource + "|expected=" + $expectedFork + "|actual=" + $forkActualSha12 + "|match=True")
    } else {
        [void]$failReasons.Add('fork-missing')
        Write-Output ("KERNEL-FORK|resolved=" + $forkPath + "|source=" + $forkSource + "|expected=" + $expectedFork + "|actual=" + $forkActualSha12 + "|match=False|reason=" + $LabelForkMissing + "|note=the object at that path is NOT the expected fork dll")
    }
}

# ---------------------------------------------------------------- decoy standing
# The task's invariant, stated as machine-checkable fields: the top-level AIPlayer.MpvHost.dll in a
# release tree is the ORIGINAL stock dll ("decoy"); its presence must never be credited as a pass.
if (Test-Path -LiteralPath $topKernelDllPath) {
    $topKernelDllSha12 = Get-FileSha12 $topKernelDllPath
    $topKernelDllItem = Get-Item -LiteralPath $topKernelDllPath
    if ($topKernelDllSha12 -eq $expectedFork) {
        Write-Output ("DECOY-STANDING|topDll=present|sha12=" + $topKernelDllSha12 + "|role=TOP-IS-FORK(kernel-stage layout)|countsAsPass=False|credit=sha12-match-only|label=TOP-IS-FORK")
        Write-Output ("DECOY-NOTE|no top-level decoy in this tree: the top-level dll IS the expected fork (sha12 match). The pass still comes from the sha12 match, never from presence.")
    } else {
        $originalStock = $OriginalStockSha12.ToUpperInvariant()
        $topDllRole = if ($originalStock -match '^[0-9A-F]{12}$' -and $topKernelDllSha12 -eq $originalStock) { 'original-stock' } else { 'not-expected-fork' }
        Write-Output ("DECOY-STANDING|topDll=present|sha12=" + $topKernelDllSha12 + "|bytes=" + $topKernelDllItem.Length + "|role=" + $topDllRole + "|countsAsPass=False|credit=sha12-match-only")
        Write-Output ("DECOY|topDll=AIPlayer.MpvHost.dll|label=" + $LabelDecoy + "|mustNotBeLoaded=True|reason=top-level dll is not the expected fork (sha12 mismatch)")
        Write-Output ("DECOY-NOTE|the top-level AIPlayer.MpvHost.dll must not be loaded; PASS credit comes only from the sha12 match, never from presence")
    }
} else {
    Write-Output ("DECOY-STANDING|topDll=absent|countsAsPass=False|credit=sha12-match-only|note=no top-level AIPlayer.MpvHost.dll in this tree")
}

# ---------------------------------------------------------------- topology: three counts, printed separately
$topFileCount = [IO.Directory]::GetFiles($distRoot, '*', [IO.SearchOption]::TopDirectoryOnly).Count
$recursiveFileCount = Measure-FilesRecursive $distRoot
$playerFileCount = if ($playerDirExists) { Measure-FilesRecursive $playerDir } else { 0 }
$newestDistMtime = if ($recursiveFileCount -gt 0) { (Get-ChildItem -LiteralPath $distRoot -Recurse -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') } else { '(none)' }
Write-Output ("TOPOLOGY|topFiles=" + $topFileCount + "|recursiveFiles=" + $recursiveFileCount + "|playerFiles=" + $playerFileCount + "|playerDirExists=" + $playerDirExists + "|newestDistMtime=" + $newestDistMtime)

# ---------------------------------------------------------------- kernel trio presence
# dll + native + pri. Scope follows the layout: player\ when it exists, else the top level.
$trioScope = if ($playerDirExists) { $playerDir } else { $distRoot }
$trioScopeName = if ($playerDirExists) { 'player' } else { 'top' }
$trioNames = @('AIPlayer.MpvHost.dll', 'libmpv-2.dll', 'AIPlayer.MpvHost.pri')
$trioRoles = @('dll', 'native', 'pri')
$trioMissing = New-Object System.Collections.ArrayList
for ($trioIndex = 0; $trioIndex -lt $trioNames.Count; $trioIndex++) {
    $trioName = $trioNames[$trioIndex]
    $trioRole = $trioRoles[$trioIndex]
    $trioPath = Join-Path $trioScope $trioName
    if (Test-Path -LiteralPath $trioPath) {
        $trioItem = Get-Item -LiteralPath $trioPath
        Write-Output ("KERNEL-TRIO|scope=" + $trioScopeName + "|role=" + $trioRole + "|name=" + $trioName + "|present=True|bytes=" + $trioItem.Length)
    } else {
        [void]$trioMissing.Add($trioName)
        Write-Output ("KERNEL-TRIO|scope=" + $trioScopeName + "|role=" + $trioRole + "|name=" + $trioName + "|present=False")
    }
}
if ($trioMissing.Count -gt 0) {
    [void]$failReasons.Add('kernel-trio-incomplete')
    Write-Output ("KERNEL-TRIO-VERDICT|scope=" + $trioScopeName + "|missing=" + ($trioMissing -join ',') + "|verdict=FAIL")
} else {
    Write-Output ("KERNEL-TRIO-VERDICT|scope=" + $trioScopeName + "|missing=0|verdict=PASS")
}

# ---------------------------------------------------------------- build-face hygiene: zero-byte *.g.cs
# WHY this is a gate: a concurrent WinUI3 build can leave App.g.cs / MainWindow.g.cs / XamlTypeInfo.g.cs
# / PlaceholderPage.g.cs at 0 bytes; the next build then dies with MSB3073 and NO XAML diagnostic.
#
# WHY a single sample is not enough (measured 2026-09-12 17:26:51, shell/App/obj): XamlCompiler truncates
# these files to 0 bytes, then writes them back minutes later. A one-shot read taken in that window
# reported 16 zero-byte files while two reads taken 40 s apart - by Get-ChildItem and by
# [IO.Directory]::GetFiles - both reported 0. So the reading is only usable when (a) no build is in
# flight and (b) two samples agree. Otherwise this face reports INCONCLUSIVE, never a silent PASS and
# never a fabricated FAIL. The 0-byte budget itself is unchanged: a stable, build-free reading over
# budget is a FAIL.
$zeroByteGcsTotal = 0
$hygieneInconclusive = New-Object System.Collections.ArrayList
$buildProcessNames = @('XamlCompiler', 'MSBuild', 'dotnet')
$buildProcesses = @(Get-Process -Name $buildProcessNames -ErrorAction SilentlyContinue)
# VBCSCompiler is a persistent compiler server (started long before any single build), so it is
# excluded on purpose: counting it would pin this face at INCONCLUSIVE for the whole session.
$buildInFlight = ($buildProcesses.Count -gt 0)
$buildInFlightPids = if ($buildInFlight) { (($buildProcesses | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ',') } else { '(none)' }
Write-Output ("BUILD-INFLIGHT|inFlight=" + $buildInFlight + "|procs=" + $buildInFlightPids + "|sampledAt=" + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff'))

foreach ($objDir in $ProjectObjDir) {
    if (-not (Test-Path -LiteralPath $objDir)) {
        [void]$hygieneInconclusive.Add('obj-dir-absent:' + $objDir)
        Write-Output ("BUILD-HYGIENE|objDir=" + $objDir + "|present=False|verdict=INCONCLUSIVE|note=obj dir does not exist; nothing was read (this is NOT a pass)")
        continue
    }
    $sampleOneAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
    $sampleOne = Get-GeneratedSourceReading $objDir
    if ($SettleMs -gt 0) { Start-Sleep -Milliseconds $SettleMs }
    $sampleTwoAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
    $sampleTwo = Get-GeneratedSourceReading $objDir

    $stableReading = ($sampleOne.Zero -eq $sampleTwo.Zero) -and (($sampleOne.ZeroPaths -join '|') -eq ($sampleTwo.ZeroPaths -join '|'))
    $hygieneVerdict = 'PASS'
    if ($buildInFlight) { $hygieneVerdict = 'INCONCLUSIVE' }
    elseif (-not $stableReading) { $hygieneVerdict = 'INCONCLUSIVE' }
    elseif ($sampleTwo.Zero -gt $MaxZeroByteGcs) { $hygieneVerdict = 'FAIL' }

    Write-Output ("BUILD-HYGIENE|objDir=" + $objDir + "|gcsTotal=" + $sampleTwo.Total + "|zeroByteGcs=" + $sampleTwo.Zero + "|budget=" + $MaxZeroByteGcs + "|sample1At=" + $sampleOneAt + "|sample1Zero=" + $sampleOne.Zero + "|sample2At=" + $sampleTwoAt + "|sample2Zero=" + $sampleTwo.Zero + "|stable=" + $stableReading + "|newestGcsMtime=" + $sampleTwo.NewestMtime + "|verdict=" + $hygieneVerdict)
    foreach ($zeroPath in $sampleTwo.ZeroPaths) {
        Write-Output ("FINDING|BUILD-HYGIENE|zero-byte-generated-source|" + $zeroPath + "|advice=delete the 0-byte *.g.cs (+ XamlSaveStateFile.xml) and rebuild; a concurrent build clobbers these files")
    }
    if ($hygieneVerdict -eq 'FAIL') {
        $zeroByteGcsTotal += $sampleTwo.Zero
        [void]$failReasons.Add('zero-byte-generated-source')
    } elseif ($hygieneVerdict -eq 'INCONCLUSIVE') {
        [void]$hygieneInconclusive.Add($(if ($buildInFlight) { 'build-in-flight(' + $buildInFlightPids + ')' } else { 'obj-tree-unstable:' + $objDir }))
    }
}
$hygieneOverall = if ($failReasons -contains 'zero-byte-generated-source') { 'FAIL' } elseif ($hygieneInconclusive.Count -gt 0) { 'INCONCLUSIVE' } else { 'PASS' }
Write-Output ("BUILD-HYGIENE-VERDICT|zeroByteGcsTotal=" + $zeroByteGcsTotal + "|budget=" + $MaxZeroByteGcs + "|verdict=" + $hygieneOverall + "|note=INCONCLUSIVE means the build face could not be read (build in flight or unstable obj tree); it is never reported as PASS")

# XamlSaveStateFile.xml: printed, NOT gated. The runbook for the MSB3073 hazard is "delete it and
# rebuild", so its presence is not itself a defect; gating it would be a permanent false red.
$xamlStateFiles = @(Get-ChildItem -LiteralPath $ProjectObjDir[0] -Recurse -Filter 'XamlSaveStateFile.xml' -File -ErrorAction SilentlyContinue)
if ($xamlStateFiles.Count -eq 0) {
    Write-Output ("XAML-SAVE-STATE|objDir=" + $ProjectObjDir[0] + "|count=0|gated=False|note=absent is not a defect")
} else {
    foreach ($xamlStateFile in $xamlStateFiles) {
        Write-Output ("XAML-SAVE-STATE|path=" + $xamlStateFile.FullName + "|bytes=" + $xamlStateFile.Length + "|mtime=" + $xamlStateFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff') + "|gated=False|note=presence alone is not a defect; it is deleted as part of the rebuild recipe")
    }
}

# Disclosed, not gated: sibling project obj dirs can hold zero-byte .g.cs too. They are outside the
# release face, so they cannot fail this run - but they are named, with the exact command to gate them.
$shellRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
foreach ($siblingProject in @(Get-ChildItem -LiteralPath $shellRoot -Directory -ErrorAction SilentlyContinue)) {
    $siblingObjDir = Join-Path $siblingProject.FullName 'obj'
    if (-not (Test-Path -LiteralPath $siblingObjDir)) { continue }
    if ($ProjectObjDir -contains $siblingObjDir) { continue }
    $siblingGcs = @(Get-ChildItem -LiteralPath $siblingObjDir -Recurse -Filter '*.g.cs' -File -ErrorAction SilentlyContinue)
    $siblingZero = @($siblingGcs | Where-Object { $_.Length -eq 0 })
    if ($siblingZero.Count -gt 0) {
        Write-Output ("NOTE|hygiene-elsewhere|objDir=" + $siblingObjDir + "|zeroByteGcs=" + $siblingZero.Count + "|gated=False|reason=outside the release face|to-gate=-ProjectObjDir " + $siblingObjDir)
    }
}

# ---------------------------------------------------------------- Assets parity
# Static counterpart of the runtime 'SERVERS icon-missing' log line: it answers "would the shipped
# Assets tree satisfy every asset the source tree declares". It is NOT the runtime count - that one
# needs a live shell with a populated rail - so a 0 here must not be read as "the runtime log was 0".
$distAssetsDir = Join-Path $distRoot 'Assets'
$distAssetsCount = if (Test-Path -LiteralPath $distAssetsDir) { Measure-FilesRecursive $distAssetsDir } else { 0 }
$assetMissing = New-Object System.Collections.ArrayList
$assetChecked = 0
# The parity check is only meaningful on the release face: a kernel-stage tree never ships shell assets,
# so comparing it there would print a red-looking count for a check that does not apply. When the shape
# is not 'release' the count is reported as n/a rather than 0 - a 0 here would be an empty reading.
$assetComparable = ($shape -eq 'release')
if ($assetComparable -and (Test-Path -LiteralPath $SourceAssetsDir)) {
    $sourceAssetFiles = [IO.Directory]::GetFiles($SourceAssetsDir, '*', [IO.SearchOption]::AllDirectories)
    foreach ($sourceAssetFile in $sourceAssetFiles) {
        $assetChecked++
        $assetRel = $sourceAssetFile.Substring($SourceAssetsDir.Length).TrimStart('\')
        $assetDistPath = Join-Path $distAssetsDir $assetRel
        if (-not (Test-Path -LiteralPath $assetDistPath)) {
            [void]$assetMissing.Add($assetRel)
            Write-Output ("ASSETS-MISSING|rel=" + $assetRel + "|expectedBytes=" + (Get-Item -LiteralPath $sourceAssetFile).Length + "|reason=absent-in-dist")
        } else {
            $assetDistItem = Get-Item -LiteralPath $assetDistPath
            $assetSourceItem = Get-Item -LiteralPath $sourceAssetFile
            if ($assetDistItem.Length -ne $assetSourceItem.Length) {
                [void]$assetMissing.Add($assetRel)
                Write-Output ("ASSETS-MISSING|rel=" + $assetRel + "|expectedBytes=" + $assetSourceItem.Length + "|actualBytes=" + $assetDistItem.Length + "|reason=size-mismatch")
            }
        }
    }
}
$assetScope = if ($assetComparable) { 'release' } else { 'not-applicable(' + $shape + ' tree)' }
$assetIconMissingField = if ($assetComparable) { [string]$assetMissing.Count } else { 'n/a' }
Write-Output ("ASSETS|distAssetsDir=" + $distAssetsDir + "|distAssetsFiles=" + $distAssetsCount + "|sourceAssetsDir=" + $SourceAssetsDir + "|sourceAssetsChecked=" + $assetChecked + "|iconMissing=" + $assetIconMissingField + "|scope=" + $assetScope + "|note=static parity count, NOT the runtime 'SERVERS icon-missing' log line (that one needs a live shell with a populated rail)")
if ($assetComparable -and $assetMissing.Count -gt 0) { [void]$failReasons.Add('icon-missing-in-dist') }

# ---------------------------------------------------------------- verdict
# verdict=PASS requires fail=0 AND inconclusive=0. A face that could not be read is never folded into
# a pass: SUMMARY keeps the two counts apart ('fail=0|inconclusive=1' means "nothing proven bad, but
# the build face was unreadable"), and only verdict=PASS exits 0.
$allReasons = @($failReasons) + @($hygieneInconclusive)
$summaryFields = "shape=" + $shape + "|dist=" + $distRoot + "|forkSha12=" + $expectedFork + "|forkSource=" + $(if ($forkSource -eq '') { '<none>' } else { $forkSource }) + "|topFiles=" + $topFileCount + "|recursiveFiles=" + $recursiveFileCount + "|playerFiles=" + $playerFileCount + "|trioMissing=" + $trioMissing.Count + "|zeroByteGcs=" + $zeroByteGcsTotal + "|iconMissing=" + $assetIconMissingField + "|fail=" + $failReasons.Count + "|inconclusive=" + $hygieneInconclusive.Count
if ($failReasons.Count -eq 0 -and $hygieneInconclusive.Count -eq 0) {
    Write-Output ("SUMMARY|verdict=PASS|" + $summaryFields)
    exit 0
} else {
    Write-Output ("SUMMARY|verdict=FAIL|" + $summaryFields + "|reasons=" + ($allReasons -join ';'))
    exit 1
}
