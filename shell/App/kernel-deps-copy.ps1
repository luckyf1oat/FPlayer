<#
  kernel-deps-copy.ps1 —— 把「外壳复用内核」所需的程序集从 data/player 复制到指定输出目录。
  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File kernel-deps-copy.ps1 -TargetDir "<输出目录>"
    powershell -NoProfile -ExecutionPolicy Bypass -File kernel-deps-copy.ps1 -TargetDir "<输出目录>" -Verify
    powershell -NoProfile -ExecutionPolicy Bypass -File kernel-deps-copy.ps1 -TargetDir "<输出目录>" -IncludeWinAppSdk
  退出码：0=就位  2=清单内任一侧缺失（源缺失=clean 清单/发布目录不完整；目标缺失=尚未复制或已被构建冲掉）  3=复制失败（多为并发构建锁文件，稍后重跑）
#>
param(
    # 默认源目录按**脚本位置**解析（脚本在 shell/App/ 下，data/player 在仓库根）。
    # 写死绝对路径会让任何非默认 checkout（worktree / CI / 他人机器）直接 MSB3073。
    [string]$SourceDir = (Join-Path $PSScriptRoot '..\..\data\player'),
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [switch]$Verify,
    [switch]$IncludeWinAppSdk,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

# ⚠️ 必须去掉结尾的分隔符：MSBuild 的 $(TargetDir) / $(PublishDir) **都以反斜杠结尾**，
#    而 `-TargetDir "…\win-x64\"` 里的 `\"` 会被 PowerShell 当成**转义引号**，
#    导致引号不配对、参数解析崩掉（症状：脚本在第 23 行附近报语法/参数错，MSB3073 exit 1）。
$TargetDir = $TargetDir.TrimEnd('\', '/')

# 内核运行时动态加载、但不登记在 deps.json 的项（必须显式搬运，glob 猜不到）
# [t136] `libmpv-2.dll` **已从本清单移除** —— 外壳对 libmpv 的解析是**三段链**
#     `SHELL_KERNEL_LIBMPV` > `<BaseDir>\player\libmpv-2.dll` > legacy 绝对路径
#     （KernelLauncher.cs:99-114 `Resolve()`）⇒ **输出根从来不在链上**；
#     根副本是 ~118 MB 纯冗余，并且它正是"顶层同名 libmpv-2.dll 到底是哪一份"的歧义源
#     （判定依据是 `KERNEL-LIBMPV … source=player`，不是"哪个文件在不在"）。
$mustCopyExplicit = @('Microsoft.WindowsAppRuntime.dll')

# [t136] 闭包里的**内核自身镜像**同样不落输出根：deps.json 里它以库 `AIPlayer.MpvHost/1.0.0`
#     （`runtime: AIPlayer.MpvHost.dll`）出现 ⇒ 旧版会把它复制到输出根（原版 979,456 B / 8D73526C2C07）。
#     内核镜像的解析走**同一条三段链**、根不在链上；外壳产物实际用的是 `<BaseDir>\player\` 里的 fork 内核
#     ⇒ 根上的原版 dll 只会制造"跑的到底是哪一份"的误读。
$excludeFromClosure = @('AIPlayer.MpvHost.dll')

$depsPath = Join-Path $SourceDir 'AIPlayer.MpvHost.deps.json'
if (-not (Test-Path $depsPath)) { throw "找不到内核依赖清单：$depsPath" }
if (-not (Test-Path $TargetDir)) { throw "输出目录不存在：$TargetDir" }

$deps = Get-Content $depsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$targetName = 'win-x64'
foreach ($n in $deps.targets.PSObject.Properties.Name) { if ($n -like '*/win-x64') { $targetName = $n; break } }
$target = $deps.targets.$targetName
if (-not $target) { throw "deps.json 中无 win-x64 目标" }

$core = New-Object System.Collections.Generic.List[string]    # 内核自身+其依赖（本脚本负责）
$wasdk = New-Object System.Collections.Generic.List[string]   # WinAppSDK 受管投影（SelfContained 负责）

foreach ($lib in $target.PSObject.Properties) {
    if ($lib.Name -like 'runtimepack.Microsoft.NETCore.App*') { continue }        # .NET BCL
    if ($lib.Name -like 'runtimepack.Microsoft.Windows.SDK.NET.Ref*') { continue }# 编译期引用包
    $bucket = $core
    if ($lib.Name -like 'Microsoft.WindowsAppSDK*') { $bucket = $wasdk }
    if ($lib.Value.runtime) { foreach ($e in $lib.Value.runtime.PSObject.Properties) { $bucket.Add((Split-Path $e.Name -Leaf)) } }
    if ($lib.Value.native)  { foreach ($e in $lib.Value.native.PSObject.Properties)  { $bucket.Add((Split-Path $e.Name -Leaf)) } }
}
foreach ($n in $mustCopyExplicit) { $core.Add($n) }

$coreList  = @($core  | Sort-Object -Unique | Where-Object { $excludeFromClosure -notcontains $_ })
$wasdkList = @($wasdk | Sort-Object -Unique)

# 框架提供的程序集：由宿主所用的 .NET 共享框架解析 ⇒ **永远不是手工复制项**
#   （框架依赖模式：框架提供；自包含发布：runtimepack 提供，见 data/player 的那一份即由 runtimepack 落下）
# 本机实测（2026-09-11）：Microsoft.NETCore.App（8.0.23/8.0.25/10.0.8）里含 System.Diagnostics.DiagnosticSource.dll
#   ⇒ 它是唯一一项「按内核 deps.json 在清单里、但输出目录不需要存在」的项。
# 不排除它会导致对框架依赖宿主**永久报缺 1 项（假阳性）**，把真缺项淹没（ui 2026-09-11 指出；其 Spike 无该文件仍实跑为绿）。
# 注意：**不能按「任一共享框架」一刀切** —— System.Drawing.Common / Microsoft.Win32.SystemEvents /
#   System.Private.Windows.Core 只在 Microsoft.WindowsDesktop.App 里，而 WinUI3 宿主用不到该框架，故它们**仍需保留**。
$frameworkProvided = New-Object System.Collections.Generic.List[string]
$fwRoot = 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App'
if (Test-Path $fwRoot) {
    $fwFiles = @{}
    Get-ChildItem $fwRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        Get-ChildItem $_.FullName -Filter *.dll -File -ErrorAction SilentlyContinue | ForEach-Object { $fwFiles[$_.Name] = $true }
    }
    foreach ($n in $coreList) { if ($fwFiles.ContainsKey($n)) { $frameworkProvided.Add($n) } }
}
$requiredList = @($coreList | Where-Object { -not $frameworkProvided.Contains($_) })

$plan = New-Object System.Collections.Generic.List[string]
$plan.AddRange([string[]]$requiredList)
if ($IncludeWinAppSdk) { $plan.AddRange([string[]]$wasdkList) }

$missingSrc = New-Object System.Collections.Generic.List[string]  # 源（data/player）缺失
$missingDst = New-Object System.Collections.Generic.List[string]  # 目标（输出目录）缺失
$failed  = New-Object System.Collections.Generic.List[string]
$copied = 0; $skippedSame = 0

foreach ($name in $plan) {
    $src = Join-Path $SourceDir $name
    if (-not (Test-Path $src)) { $missingSrc.Add($name); continue }
    $dst = Join-Path $TargetDir $name
    if ($Verify -or $DryRun) { if (Test-Path $dst) { $skippedSame++ } else { $missingDst.Add($name) }; continue }
    # 目标已存在且大小一致 ⇒ 视为就位（不必要复制，也避开并发构建的瞬时文件锁）
    if (Test-Path $dst) {
        if ((Get-Item -LiteralPath $src).Length -eq (Get-Item -LiteralPath $dst).Length) { $skippedSame++; continue }
    }
    $ok = $false
    for ($a = 0; $a -lt 3 -and -not $ok; $a++) {
        try { Copy-Item -LiteralPath $src -Destination $dst -Force -ErrorAction Stop; $ok = $true }
        catch { Start-Sleep -Milliseconds (300 * ($a + 1)) }
    }
    if ($ok) { $copied++ } else { $failed.Add($name) }
}

Write-Output "内核依赖复制/校验 —— 源=$SourceDir"
Write-Output "  目标=$TargetDir"
Write-Output "  模式=$(if($Verify){'Verify'}elseif($DryRun){'DryRun'}else{'Copy'})；包含 WinAppSDK 受管投影=$([bool]$IncludeWinAppSdk)"
Write-Output "  清单：需随输出存在 $($requiredList.Count) 项$(if($IncludeWinAppSdk){" + WinAppSDK $($wasdkList.Count) 项"})"
if ($frameworkProvided.Count -gt 0) {
    Write-Output "  框架提供（Microsoft.NETCore.App，**输出目录无需存在**）：$($frameworkProvided -join ', ')"
}
if ($Verify -or $DryRun) { Write-Output "  已存在 $skippedSame 项" } else { Write-Output "  已复制 $copied 项 / 已就位 $skippedSame 项" }
# 两侧必须分开报：源缺失 = 清单本身有问题（data/player 不完整）；目标缺失 = 尚未复制，或**已被下一次构建冲掉**。
# （早期版本把两者混在一个列表里、统一写「源缺失」，会把人引向错误方向 —— 2026-09-11 实测自查发现并修正。）
if ($missingSrc.Count -gt 0) {
    Write-Output "  !! 源缺失 $($missingSrc.Count) 项（data/player 里没有，清单或原版发布目录不完整）："
    $missingSrc | ForEach-Object { Write-Output "     - $_" }
}
if ($missingDst.Count -gt 0) {
    Write-Output "  !! 目标缺失 $($missingDst.Count) 项（输出目录里没有 ⇒ 尚未复制，或已被后续构建冲掉）："
    $missingDst | ForEach-Object { Write-Output "     - $_" }
}
if ($missingSrc.Count -gt 0 -or $missingDst.Count -gt 0) { exit 2 }
if ($failed.Count -gt 0) {
    Write-Output "  !! 复制失败 $($failed.Count) 项（多为并发构建锁住目标文件；稍后重跑即可）："
    $failed | ForEach-Object { Write-Output "     - $_" }
    exit 3
}
Write-Output "  结果：清单内全部就位 ✅"
exit 0
