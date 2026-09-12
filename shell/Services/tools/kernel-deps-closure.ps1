<#
  kernel-deps-closure.ps1 —— 内核依赖闭包分析（可执行清单生成器）

  兼容性：Windows PowerShell 5.1 与 PowerShell 7 均可运行（不使用 7.0 专有语法/编码参数）。
  ⚠️ 本文件必须保存为 **UTF-8 with BOM**：PS 5.1 对无 BOM 的 UTF-8 脚本按 ANSI(GBK) 解码，会把中文弄成乱码。

  用途：把「外壳输出目录里到底要放哪些文件」算清楚，分四面：
        (A) deps.json 声明的**托管**依赖（NuGet 面）
        (B) deps.json 声明的 **native** 资产
        (C) 内核源码里**运行期动态加载**的 native（DllImport / NativeLibrary.Load / libmpv）—— deps.json 根本不登记
        (D) data/player 里存在、但不属上述任何一类的文件（WinAppSDK native / 其它 native）

  背景（captain 交办 + ui 实测）：.NET 默认装载器**只对登记在 <App>.deps.json 里的程序集探测应用目录**；
  post-build 复制进来的 DLL 探测不到 ⇒ 症状是「文件明明在输出目录，却抛 FileNotFoundException /
  stowed exception 0xC000027B（无事件日志、无 WER）」。

  输出：
    <OutDir>/KERNEL_DEPENDENCY_CLOSURE.md    人读报告（UTF-8 with BOM）
    <OutDir>/kernel-deps-closure.json        机读清单（UTF-8 无 BOM）

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File shell\Services\tools\kernel-deps-closure.ps1
#>
param(
    [string]$PlayerDir = 'E:\AI Player\data\player',
    [string[]]$KernelSrcDirs = @('E:\AI Player\reversed\MpvHost', 'E:\AI Player\reversed\ThirdParty'),
    [string]$OutDir = 'E:\AI Player\shell\Services\docs'
)

$ErrorActionPreference = 'Stop'

function Get-LibraryKind {
    param([string]$Name)
    if ($Name -like 'runtimepack.Microsoft.NETCore.App.Runtime*') { return 'BclRuntime' }
    if ($Name -like 'runtimepack.Microsoft.Windows.SDK.NET.Ref*') { return 'WinSdkRef' }
    if ($Name -like 'Microsoft.WindowsAppSDK*') { return 'WinAppSdk' }
    if ($Name -match '\.Reference/\d') { return 'LocalReference' }
    return 'ManagedDependency'
}

function Write-TextFile {
    param([string]$Path, [string]$Text, [bool]$WithBom)
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($WithBom)))
}

function Read-Utf8 {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, (New-Object System.Text.UTF8Encoding($false)))
}

$depsPath = Join-Path $PlayerDir 'AIPlayer.MpvHost.deps.json'
if (-not (Test-Path $depsPath)) { throw "找不到内核依赖清单：$depsPath" }

$deps = Get-Content $depsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$targetName = 'win-x64'
foreach ($n in $deps.targets.PSObject.Properties.Name) {
    if ($n -like '*/win-x64') { $targetName = $n; break }
}
$target = $deps.targets.$targetName
if (-not $target) { throw "deps.json 中没有 win-x64 目标" }

# ── data/player 实际落地文件（按文件名索引）────────────────────────────────
$present = @{}
Get-ChildItem $PlayerDir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    if (-not $present.ContainsKey($_.Name)) { $present[$_.Name] = $_.FullName }
}

# ── (A) 托管依赖 ───────────────────────────────────────────────────────────
$rows = New-Object System.Collections.Generic.List[object]
foreach ($lib in $target.PSObject.Properties) {
    $kind = Get-LibraryKind $lib.Name
    $runtime = $lib.Value.runtime
    if (-not $runtime) { continue }
    foreach ($entry in $runtime.PSObject.Properties) {
        $leaf = Split-Path $entry.Name -Leaf
        $rows.Add((New-Object psobject -Property @{
            Library = $lib.Name; Kind = $kind; File = $leaf; InPlayer = $present.ContainsKey($leaf)
        }))
    }
}

# ── (B) deps.json 声明的 native ────────────────────────────────────────────
$nativeRows = New-Object System.Collections.Generic.List[object]
foreach ($lib in $target.PSObject.Properties) {
    $native = $lib.Value.native
    if (-not $native) { continue }
    foreach ($entry in $native.PSObject.Properties) {
        $leaf = Split-Path $entry.Name -Leaf
        $nativeRows.Add((New-Object psobject -Property @{
            Library = $lib.Name; File = $leaf; RelPath = $entry.Name; InPlayer = $present.ContainsKey($leaf)
        }))
    }
}

# ── (C) 内核源码里运行期动态加载的 native ─────────────────────────────────
$dynNames = @{}
foreach ($dir in $KernelSrcDirs) {
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in (Get-ChildItem $dir -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue)) {
        $txt = ''
        try { $txt = Read-Utf8 -Path $f.FullName } catch { continue }
        foreach ($m in [regex]::Matches($txt, 'DllImport\(\s*"([^"]+)"')) { $dynNames[$m.Groups[1].Value] = $f.Name }
        foreach ($m in [regex]::Matches($txt, 'NativeLibrary\.(?:Load|TryLoad)\(\s*"([^"]+)"')) { $dynNames[$m.Groups[1].Value] = $f.Name }
        foreach ($m in [regex]::Matches($txt, '"(libmpv[^"]*\.dll)"')) { $dynNames[$m.Groups[1].Value] = $f.Name }
    }
}
$dynRows = New-Object System.Collections.Generic.List[object]
foreach ($k in ($dynNames.Keys | Sort-Object)) {
    $dynRows.Add((New-Object psobject -Property @{
        File = $k; SeenIn = $dynNames[$k]; InPlayer = $present.ContainsKey($k)
    }))
}
$dynMustCopy = @($dynRows | Where-Object { $_.InPlayer })

# ── (D) 其余落地的 .dll（既不在托管面也不在 native 面）────────────────────
$declared = @{}
foreach ($r in $rows) { $declared[$r.File] = $true }
foreach ($r in $nativeRows) { $declared[$r.File] = $true }
$allDlls = @(Get-ChildItem $PlayerDir -Recurse -File -Filter *.dll -ErrorAction SilentlyContinue)
$others = @($allDlls | Where-Object { -not $declared.ContainsKey($_.Name) } | Sort-Object Name)
$othersNames = @($others | Select-Object -ExpandProperty Name)

$byKind = $rows | Group-Object Kind | Sort-Object Name
$managedNames = @($rows | Where-Object { $_.Kind -eq 'ManagedDependency' } | Select-Object -ExpandProperty File | Sort-Object -Unique)
$localNames = @($rows | Where-Object { $_.Kind -eq 'LocalReference' } | Select-Object -ExpandProperty File | Sort-Object -Unique)
$winAppSdkNames = @($rows | Where-Object { $_.Kind -eq 'WinAppSdk' } | Select-Object -ExpandProperty File | Sort-Object -Unique)
$missingManaged = @($managedNames | Where-Object { -not $present.ContainsKey($_) })
$missingLocal = @($localNames | Where-Object { -not $present.ContainsKey($_) })
$probeList = @(@($managedNames) + @($localNames) | Sort-Object -Unique)
$missingNative = @($nativeRows | Where-Object { -not $_.InPlayer })

$stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
$L = New-Object System.Collections.Generic.List[string]
$L.Add('# 内核依赖闭包清单（自动生成，勿手改）')
$L.Add('')
$L.Add("> 生成时间：$stamp ｜ 生成脚本：``shell/Services/tools/kernel-deps-closure.ps1``")
$L.Add("> 权威依据：(1) ``data/player/AIPlayer.MpvHost.deps.json``（内核发布依赖清单，$($deps.libraries.PSObject.Properties.Name.Count) 个 library）")
$L.Add('>            (2) `reversed/MpvHost/**/*.cs` + `reversed/ThirdParty/**/*.cs`（源码里的 `DllImport` / `NativeLibrary.Load` / `libmpv-2.dll` 字面量）')
$L.Add("> 目标 RID：``$targetName``")
$L.Add('')
$L.Add('## 0. 为什么需要这份清单（一句话机制）')
$L.Add('')
$L.Add('**.NET 默认装载器只对登记在 `<App>.deps.json` 里的程序集探测应用目录。**')
$L.Add('⇒ post-build 复制（或手工拷贝）进来的托管 DLL **探测不到**，症状是「文件明明在输出目录，却抛')
$L.Add('`FileNotFoundException` / stowed exception `0xC000027B`（无事件日志、无 WER）」。')
$L.Add('⇒ 两条正解：**(1)** 走 `PackageReference`（NuGet 自动写 deps.json）；**(2)** 手工 `Reference` + 显式')
$L.Add('`AppDomain.AssemblyResolve` / `AssemblyLoadContext.Resolving` 从句 `AppContext.BaseDirectory` 解析。')
$L.Add('⇒ 而 **native** 库（`libmpv-2.dll` 等）**不经过这套机制**，必须实打实放在输出目录里 —— 见 §4。')
$L.Add('')
$L.Add('## 1. 分类统计')
$L.Add('')
$L.Add('| 类别 | 含义 | 库数 | 文件数 | 是否需手工复制 |')
$L.Add('|---|---|---|---|---|')
$kindMeta = @{
    'ManagedDependency' = @('内核的托管依赖（NuGet 包 DLL）', '否 —— 由 PackageReference / Resolving 解决')
    'LocalReference'    = @('本地程序集引用（`<Reference>` + HintPath，如 Danmaku / Richasy 发布版）', '是 —— NuGet 无法还原')
    'WinAppSdk'         = @('Windows App SDK 受管组件', '否 —— 由 `WindowsAppSDKSelfContained=true` 自动复制')
    'WinSdkRef'         = @('Windows SDK 投影引用包（编译期）', '否 —— 运行期不需要')
    'BclRuntime'        = @('.NET 运行时（BCL；本清单声明 9.0.14）', '否 —— 由 RollForward 或自包含发布提供')
}
foreach ($g in $byKind) {
    $meta = $kindMeta[$g.Name]
    $libCount = @($g.Group | Select-Object -ExpandProperty Library -Unique).Count
    $L.Add("| ``$($g.Name)`` | $($meta[0]) | $libCount | $($g.Count) | $($meta[1]) |")
}
$L.Add('')
$L.Add("托管面合计 **$($rows.Count)** 条 runtime 记录；去重后需能被解析的程序集名 **$($probeList.Count)** 个。")
$L.Add("native 面：deps.json 声明 **$($nativeRows.Count)** 条；内核源码动态加载名 **$($dynRows.Count)** 个（其中在 data/player 落地 **$($dynMustCopy.Count)** 个）。")
$L.Add('')
$L.Add('## 2. 本地程序集引用（必须随输出目录存在）')
$L.Add('')
if ($localNames.Count -eq 0) {
    $L.Add('（无）')
} else {
    $L.Add('| 文件 | 在 data/player 存在 |')
    $L.Add('|---|---|')
    foreach ($n in $localNames) {
        $mark = '❌ 缺失'
        if ($present.ContainsKey($n)) { $mark = '✅' }
        $L.Add("| ``$n`` | $mark |")
    }
}
$L.Add('')
$L.Add('## 3. 托管依赖（内核 NuGet 面：逐个都需能被解析）')
$L.Add('')
$L.Add('```')
foreach ($n in $managedNames) { $L.Add($n) }
$L.Add('```')
$L.Add('')
$L.Add('> ui 的实测教训：**只抄 `Richasy*` / `CommunityToolkit*` 不够** —— 依次还缺 `FluentIcons.WinUI`、')
$L.Add('> `FluentIcons.Common`、`WinUIEx`（本表逐项列出了全部 41 个，照表核对即可）。')
$L.Add('')
$L.Add('## 4. native 面')
$L.Add('')
$L.Add('### 4.1 内核源码里**运行期动态加载**的 native（deps.json 不登记，必须手工放）')
$L.Add('')
$L.Add('| 名称 | 在 data/player | 出现处（内核源码文件） | 结论 |')
$L.Add('|---|---|---|---|')
foreach ($r in $dynRows) {
    $mark = '❌ 不在 data/player'
    $verdict = '系统 DLL（由 OS 提供，无需随包）'
    if ($r.InPlayer) {
        $mark = '✅'
        $verdict = '**必须随输出目录提供**'
    } elseif ($r.File -notlike '*.dll') {
        $verdict = '**无扩展名**：加载器按平台补全（Windows 下通常即 libmpv-2.dll 一类）⇒ 需实机确认，勿当成系统 DLL'
    }
    $L.Add("| ``$($r.File)`` | $mark | ``$($r.SeenIn)`` | $verdict |")
}
$L.Add('')
$L.Add('> **`libmpv-2.dll`（约 112 MB）是本表的头号项**：它不在 `deps.json` 里、不由任何 NuGet target 复制，')
$L.Add('> 只能从 `data/player/libmpv-2.dll` 取。缺少它时**托管依赖全齐、播放却起不来或一播就崩**。')
$L.Add('')
$L.Add('### 4.2 deps.json 声明的 native 资产（按 RID 路径）')
$L.Add('')
$L.Add('| 库 | 文件 | 相对路径 | 在 data/player |')
$L.Add('|---|---|---|---|')
foreach ($r in $nativeRows) {
    $mark = '❌ 缺失'
    if ($r.InPlayer) { $mark = '✅' }
    $L.Add("| ``$($r.Library)`` | ``$($r.File)`` | ``$($r.RelPath)`` | $mark |")
}
$L.Add('')
$L.Add('> `Microsoft.ui.xaml.dll` 等 **WinAppSDK native** 不在此表（也不在 deps.json runtime 段），由')
$L.Add('> `WindowsAppSDKSelfContained=true` 的 targets 复制；前提是**显式 `<Platforms>x64</Platforms>`**')
$L.Add('> （否则 `GetWindowsAppSDKNativePlatform` 挑不到 `win10-<arch>` 的 Msix）。')
$L.Add('')
$L.Add('## 5. data/player 里其余 .dll（既不属托管面也不属 native 面）')
$L.Add('')
$L.Add("共 **$($others.Count)** 个（占 $($allDlls.Count) 个 .dll 的多数）。")
$L.Add('它们主要是 WinAppSDK native 运行库与第三方 native；**不要无脑全抄**（体积大且多数用不上），')
$L.Add('按需取：WinAppSDK 那部分交给 SelfContained；确认要用的个别库再单独复制。')
$L.Add('')
$L.Add('<details><summary>展开全部（前 60 项）</summary>')
$L.Add('')
$L.Add('```')
foreach ($n in ($othersNames | Select-Object -First 60)) { $L.Add($n) }
if ($othersNames.Count -gt 60) { $L.Add("… 其余 $($othersNames.Count - 60) 项见 kernel-deps-closure.json 的 othersNames") }
$L.Add('```')
$L.Add('</details>')
$L.Add('')
$L.Add('## 6. 存在性核对（清单声明 vs data/player 实际落地）')
$L.Add('')
$L.Add('| 项 | 值 |')
$L.Add('|---|---|')
if ($missingManaged.Count -eq 0) {
    $L.Add('| 托管依赖缺失 | 0 ✅ |')
} else {
    $L.Add("| 托管依赖缺失 | $($missingManaged.Count)：``$($missingManaged -join '``, ``')`` |")
}
if ($missingLocal.Count -eq 0) {
    $L.Add('| 本地引用缺失 | 0 ✅ |')
} else {
    $L.Add("| 本地引用缺失 | $($missingLocal.Count)：``$($missingLocal -join '``, ``')`` |")
}
$L.Add("| deps.json native 缺失 | $(if ($missingNative.Count -eq 0) { '0 ✅' } else { "$($missingNative.Count)" }) |")
$L.Add("| data/player 的 .dll 总数 | $($allDlls.Count) |")
$L.Add('')
$L.Add('## 7. 落地建议（按推荐度）')
$L.Add('')
$L.Add('1. **首选 M1（链接内核源码）**：`PackageReference` 齐全 ⇒ NuGet 自动写 `<App>.deps.json`，装载器能探测到，**无需 Resolving 补丁**；native（含 `libmpv-2.dll`）仍需按 §4 单独放。')
$L.Add('2. **M2（HintPath 引用成品 DLL）**：必须自带「完整托管闭包（§3）+ §2 本地引用 + `Resolving` 补丁」：')
$L.Add('')
$L.Add('   ```csharp')
$L.Add('   AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {')
$L.Add('       var simpleName = new AssemblyName(e.Name).Name;')
$L.Add('       var path = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");')
$L.Add('       return File.Exists(path) ? Assembly.LoadFrom(path) : null;')
$L.Add('   };')
$L.Add('   ```')
$L.Add('')
$L.Add('   > `AssemblyResolve` 只在**装载失败后**触发 ⇒ 它能让程序跑起来，但仍属事后补救；')
$L.Add('   > 若 `deps.json` 与复制文件不一致，应优先修 `deps.json`（即改走 M1）。')
$L.Add('3. **native 不适用上述机制**：§4.1 判为「必须随输出目录提供」的项（首要是 `libmpv-2.dll`）直接放文件即可，')
$L.Add('   它由内核经 P/Invoke 按名加载，不看 deps.json。')
$L.Add('4. **不要手工逐个抄 DLL 后靠运气**：这正是 ui 反复试错的路径。')
$L.Add('')
$L.Add('## 8. 机读清单')
$L.Add('')
$L.Add('同目录 `kernel-deps-closure.json` 含逐条记录，字段：`entries`(托管) / `nativeEntries` / `dynNativeEntries` /')
$L.Add('`othersNames` / `probeNames`，可直接做 CI 校验。')

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$mdPath = Join-Path $OutDir 'KERNEL_DEPENDENCY_CLOSURE.md'
$jsonPath = Join-Path $OutDir 'kernel-deps-closure.json'

Write-TextFile -Path $mdPath -Text ($L -join "`n") -WithBom $true

$payload = New-Object psobject -Property @{
    generatedAt       = (Get-Date).ToString('o')
    sourceDeps        = $depsPath
    sourceKernelSrcDirs = $KernelSrcDirs
    targetName        = $targetName
    libraryCount      = $deps.libraries.PSObject.Properties.Name.Count
    runtimeRecords    = $rows.Count
    probeNames        = $probeList
    managedNames      = $managedNames
    localRefNames     = $localNames
    winAppSdkNames    = $winAppSdkNames
    missingManaged    = $missingManaged
    missingLocal      = $missingLocal
    nativeEntries     = $nativeRows
    dynNativeEntries  = $dynRows
    dynNativeMustCopy = @($dynMustCopy | Select-Object -ExpandProperty File)
    othersNames       = $othersNames
    entries           = $rows
}
Write-TextFile -Path $jsonPath -Text (($payload | ConvertTo-Json -Depth 6) -replace "`r`n", "`n") -WithBom $false

Write-Output "已生成："
Write-Output "  $mdPath"
Write-Output "  $jsonPath"
Write-Output ""
Write-Output "摘要：托管 runtime 记录 $($rows.Count) 条；需可解析程序集 $($probeList.Count) 个（托管 $($managedNames.Count) + 本地引用 $($localNames.Count)）"
Write-Output "      native：deps.json $($nativeRows.Count) 条；源码动态加载 $($dynRows.Count) 个（落地需提供 $($dynMustCopy.Count) 个）"
Write-Output "      其余 .dll：$($others.Count) 个"
Write-Output "缺失：托管 $($missingManaged.Count) / 本地引用 $($missingLocal.Count)"
