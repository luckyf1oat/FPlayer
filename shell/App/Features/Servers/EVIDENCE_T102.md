# EVIDENCE_T102 — 左栏离线图标改红（用户第三次裁定）

> 卡片：`t102`（Captain 改派 `ui3`）。用户原话：「**我希望绿色 emby 图标也是红色的**」「你改一个红色出来」。
> 本文件是 `ui3` 的自证账本；所有身份数字都是**当刻从盘上量出来的**（`Get-FileHash` + `Get-Item`），不是转述。

## 0. 三态声明

| 态 | 内容 |
| --- | --- |
| 逆向到了 | 不适用——`ServerRow` 是**进程内 C# 源码**（`shell/App/MainWindow.xaml.cs`），不是逆向件。图标来源 `shell/App/Assets/Icons/emby.png` 源自原版资产 `data/flutter_assets/assets/icons/emby.png`（512×423 → 64×64）。 |
| 重建实现了 | §2 的四处改动 + §3 的两处资产改名/移位。 |
| **运行验证过了** | §4：真起外壳（隔离输出目录 + 隔离数据根），**同一窗口内**同时拍到真实不可达台的红图标+红描边与可达台的绿图标，逐像素量出 28×28 行块 / 52 px 行距 / 资产精确命中。 |

结论：本条**运行验证过了**（不是"重建实现了"就算完）。

## 1. 边界披露（越界，但经 Captain 授权）

`t102` 的改动点**全部**落在 `shell/App/MainWindow.xaml.cs` —— 该文件在 `ui3` 的"绝不写"清单内（= `ui` 的领地）。

- 授权依据：Captain 的 `t102` 卡片正文**逐行**给出该文件的改动点（`:621` 拆两缓存 / `:630-648 IconSource` 按 `_ok` 选图 / `:688-695 SetStatus` 补 `Raise`）与硬禁令（禁 `{StaticResource …}` 形态、`:101` 的 2 px 描边不动），并把卡片改派给 `ui3`。
- 并发核对（改前 2026-09-12 03:2x）：该路径 `git status --porcelain` 干净、mtime `2026-09-12 03:24:08.968`、**36,241 B / `1846F60EDE70`** ⇒ 无并发写者。
- 范围自律：只动 `ServerRow` 的三处逻辑 + 一处文档注释；**未动** `shell/App/MainWindow.xaml`、`shell/App/Theme/`、`shell/App/Features/Servers/ServersPage.xaml.cs`。

## 2. 改动集（逐条）

| # | 文件 | 改前 | 改后 | 内容与理由 |
| --- | --- | --- | --- | --- |
| 1 | `shell/App/MainWindow.xaml.cs` | `private static ImageSource _iconCache;` | `_iconCacheOk` + `_iconCacheFail` | **两个态各自缓存**。只留一份缓存 ⇒ 先到的那态永久占位（探测返回后另一态拿到的还是旧图）。 |
| 2 | 同文件 `ServerRow.IconSource` | 恒取 `Assets\Icons\emby.png` | 按 `_ok` 取 `emby.png` / `emby-offline.png`；每态各自填充；失败仍 `return null` + `RAIL-ICON-LOAD-FAIL` | 用户裁定"图标本体也要分状态"。判据 `_ok` 与 `StatusBrush`/`NameBrush` **同字段同时刻** ⇒ 图标不可能与名称色/描边互相矛盾。 |
| 3 | 同文件 `ServerRow.SetStatus` | 只 `Raise` StatusBrush / NameBrush / Detail | 补 `Raise(nameof(IconSource))` | 不补则探测返回后绑定不重取图标（图标停在首帧）。 |
| 4 | 同文件 `IconSource` 文档注释 | 一次裁定 | 记两次裁定 + 形态禁令 + 未探测语义 | 防后人改成 `{StaticResource …}` 形态（探针 B：该形态在场且 ≥1 行实体化 ⇒ `0xC000027B`）。 |

> **⚠️ 本表（第 1 列为序号）曾触发门禁 H5 的"裸名"告警，且告警原文给的修法会造"假红" —— 请勿照原文改本表。**
> 成因：H5 的单元格启发式取"**第一个以数字开头的单元格**"当声明的 bytes，本表首列序号（如 `:29` 的 `2`）正好命中 ⇒ 本表行被判为"有 bytes 无 sha"的**疑似身份行**，于是发一条 `bare-name-identity-row`（**advisory，不影响裁决**）。
> **修法警告（仍然有效）**：把本表行改写成全路径**有害** —— 那会让它真正进入 H5 的比较面，把**序号列**当作 bytes 去比 ⇒ 直接 `HASH-MISMATCH` **硬红**。`verifier` 2026-09-12 用仓外合成对照实测：同一行**裸名形态**只出 `NOTE`，**全路径形态**出 `FINDING|H5|HASH-MISMATCH|expectedBytes=3` ⇒ `CHECK|H5|verdict=FAIL`。
> **当刻状态（`t216` A3 落地后，我 19:41:35 实测）**：门禁已把该告警**收窄为"确实声明了 sha12 的行"**（advice 原文已改为"write the FULL path **AND update the bytes/sha12 to the values measured NOW**；历史轮次行请**删数字**并标注"）⇒ 本表**不再被点名**（`bareRows` 由 **3 变 2**，剩下两条在 `t31-U-E-EVIDENCE.md:417/:418`）。⇒ 本表**保持现状**（captain 2026-09-12 裁定："`EVIDENCE_T102.md:29` **不改**"）。

文件身份（当刻实测）：

| 时点 | bytes | sha256_12 | lines | mtime |
| --- | --- | --- | --- | --- |
| 改前 | 36,241 | `1846F60EDE70` | 788 | 2026-09-12 03:24:08.968 |
| 改后 | **37,774** | **`470E484049F9`** | **806** | 2026-09-12 07:54:47.653 |

## 3. 资产（改名 + 移出产物）

| 动作 | 旧路径 | 新路径 | bytes / sha256_12 |
| --- | --- | --- | --- |
| `git mv` 改名 | `shell/App/Assets/Icons/emby-offline-preview.png` | `shell/App/Assets/Icons/emby-offline.png` | 2,012 B / `0FDDB4448041`（**逐字节不变**） |
| `git mv` 移出 `Assets\**\*` 拷贝面 | `shell/App/Assets/Icons/emby-offline-preview-6x.png` | `shell/App/Features/Servers/evidence/t102-emby-offline-6x.png` | 13,504 B / `1410D3C8DCBA`（**逐字节不变**） |

- 依据（csproj，非我领地，仅引用）：`shell/App/AIPlayer.Shell.csproj:321` 的 `<Content Update="Assets\**\*" …>` 会把 `Assets\` 全量拷进输出与发布；而 **`:370-373`** 的 `<Content Remove="**\evidence\**" />` / `<None Remove…` / `<EmbeddedResource Remove…` / **`<PRIResource Remove…`** 把 evidence 目录挡在产物之外 ⇒ 6 倍预览图放 evidence 才符合"只人眼判色、不进产物"。［🔴 **2026-09-12 更正**：本行原写 `:365-367` 且只列**三类**；实测 = **`:370/371/372/373` 四类**（Content / None / EmbeddedResource / **PRIResource**），**行号偏 3、类别少一**，实质结论不变。由 `verifier` 复核，已记入 `shell/docs/VERIFY_S1.md:4313`。更正前本件 = 19,306 B / `283B83AD03A4` / RAL 197（提交 `79e80b2`，可取回）。］
- 实测印证：本次构建后 `E:\ui3-search-bin\Assets\Icons\` 只有 `emby.png`(2,094 B) 与 `emby-offline.png`(2,012 B)；输出根**无** `-6x`。
- 形色基准（供复核）：`emby.png` 主色 `#52B54B`×1071 + 白 `#FFFFFF`×183；`emby-offline.png` 主色 `#B52D29`×1075 + 白×183 ⇒ 两图**同形换色**（绿色像素→红色，白 ▶ 与透明底保留）。
- **过期引用（不在我领地，报 Captain）**：`shell/docs/WORKSPACE.md:4251`、`shell/Tests/evidence/dist-cleanliness-review.txt:17-18` 与 `:155-156` 仍写旧文件名 `emby-offline-preview*.png` ⇒ 该清单需按新名重跑或加一行改名注。

## 4. 运行验证

### 4.1 构建

```
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui3-search-bin\
```
- 前置：`Get-Process dotnet` = **0**（无并发构建）。
- 全量构建（HEAD `dfc9c5b`，07:55 起）：`已成功生成。` / `1216 个警告` / **`0 个错误`**；产物 `E:\ui3-search-bin\AIPlayer.Shell.dll` = **1,742,336 B / `F60AB39E3B94` / mtime 2026-09-12 07:55:51.142** —— **本次取证所跑的就是这一支**。
- 增量复编取显式退出码（08:02）：`EXIT=0`，产物 1,745,920 B / `0AE129A85CF9` / mtime 08:02:00.145。
- [!] **两支产物的身份不得混用**：复编之间 HEAD 被队友推进（`dfc9c5b` → `0272681` → `b0903f6` → `1b7764a`），复编把队友**在途源码**一并编入 ⇒ 字节不同是正常的，与本次改动无关。

### 4.2 起壳（隔离输出 + 隔离数据根）

- 运行前现场（`shell/tools/list-instances.ps1 -Samples 3 -IntervalMs 1000`，07:56:56.590，head `0272681`）：`shells=0 kernels=0` / `FREE-FIELD|verdict=FREE-OVER-WINDOW|samples=3|freeSamples=3|allFree=True|spanSec=2.22`。
- 命令：`Start-Process E:\ui3-search-bin\AIPlayer.Shell.exe`（pid **19716**），环境 `SHELL_SINGLEINSTANCE_ALLOW_MULTI=1` + `AIPLAYER_APPDATA_ROOT=<本次运行目录>\root`。
- 数据根：从真实根 `C:\Users\Administrator\AppData\Local\AIPlayer` **只读复制** `servers.json`(1,592 B) / `accounts.json`(17,510 B) / `credentials.bin`(230 B) / `settings.json`(3,769 B) + 三个 `.migrated_*` 标记（禁迁移，保证确定性）。真根**只读、不被写**。
- 应用自证（它的日志第一行）：`2026-09-12T07:57:39.910 APPDATA-ROOT root=<本次运行目录>\root source=override portable=False legacy=C:\Users\Administrator\AppData\Roaming\AIPlayer`。
- 收尾：`Stop-Process -Id 19716`（**只杀自己那一支**）⇒ `KILLED|pid=19716`；随后 `list-instances.ps1` 确认我的 bin 无残留实例。

### 4.3 探测结论（应用自己的日志；物化件 `t102-app-log.txt`）

- `RAIL-PROBE FAIL server=…` **7 行**（6 个不同台）：`ServerF～(∠・ω< )⌒☆`、`ServerD`×2、`server-hash-id`、`ServerH`(TaskCanceledException)、`ServerJ`、`ServerL`。
- `RAIL-ICON-LOAD-FAIL` **0 行**（从物化件独立数出，不靠转述）⇒ 两个图标资产都加载成功，没有"加载失败静默降级"。

### 4.4 同屏两向对照（验收主证）

- 通道：`shell/tools/popup-capture.ps1 -Pid 19716 -Auto -ExpectClassLike '*WinUIDesktopWin32*'` —— PrintWindow（`0x2`）单通道，工具内**无屏幕拷贝、无全屏抓取、无抢焦点**；G5 读数 `focusChanged=False` / `focusStolenFromTarget=False`。
- 窗口：`AI Player`，hwnd 89525554，`1440x759`；PNG **676,377 B / sha256_12 `1C627FC35874`**（采样 2026-09-12 07:59:20.340）⇒ 物化件 `t102-rail-window.png` + 源断言 `t102-rail-window.txt`。
- 逐像素（工具 `t102-rail-icon-probe.ps1`，扫描 `x=10..50` / `y=60..758`；分类 = 绿 `G-R≥30 且 G-B≥30`、红 `R-G≥30 且 R-B≥30`）：

| 行心 y | 态 | 描边 token 命中 | 图标资产命中 | 颜色普查（top） |
| --- | --- | --- | --- | --- |
| 292.5 | 绿 | `#50B649`×153 | `#52B54B`×133 | `#50B649` `#52B54B` |
| 344.5 | 绿 | `#50B649`×153 | `#52B54B`×133 | 同上 |
| **396.5** | **红** | `#FFADB6`×153 | `#B52D29`×137 | `#FFADB6` `#B52D29` |
| 448.5 | 绿 | `#50B649`×153 | `#52B54B`×133 | 同上 |
| 500.5 | 绿 | `#50B649`×153 | `#52B54B`×133 | 同上 |
| **552.5** | **红** | `#FFADB6`×153 | `#B52D29`×137 | 同上 |
| **604.5** | **红** | `#FFADB6`×153 | `#B52D29`×137 | 同上 |
| 646.5 | 绿（被视口裁切，h=8） | `#50B649`×58 | `#52B54B`×17 | 同上 |

三条硬读数：

1. **几何对得上 XAML**：每一行块都是 **28×28**（= `MainWindow.xaml:101` 的 `Border Width=28 Height=28 BorderThickness=2`），相邻行心距**逐对 = 52 px**（= `:85` 的 `Grid Height=52`）⇒ 8 行连续、无重叠、无错位。
2. **描边与图标同状态**：红行的 `TOKEN` 行是 `ServerFail#FFADB6=153 / ServerOk=0`，绿行是 `ServerOk#50B649=153 / ServerFail=0` ⇒ 不存在"红描边配绿图标"的串色。红行同时命中**失败token**（`#FFADB6` = `ServerFailColor`，Token 原始值 `shell/App/Theme/Tokens.xaml:28`）与**红图标资产**（`#B52D29`）⇒ 用户要的"红描边 + 红图标"两者都在。
3. **资产精确命中**：整列命中绿资产 `#52B54B` **549 px**、红资产 `#B52D29` **411 px**；549 = 133×4 + 17（第 8 行被裁切），411 = 137×3 ⇒ 屏上像素**就是这两个资产本体**，不是"碰巧是绿色/红色的别的像素"。
4. 绿行 `#50B649` 每行恰好 153 px、红行 `#FFADB6` 每行恰好 153 px —— 两态描边**像素数逐字相等**，说明描边几何在两态完全一致（只换色）。

人眼对照件：`t102-rail-strip.png`（左栏原分辨率条带，300×699，裁自 y=60）。行序与态一一对应：

| 行 | 名称 | 态 | 该台日志判据 |
| --- | --- | --- | --- |
| 1 | `ServerA` | 绿（34 天前看过） | 无 FAIL 行 |
| 2 | `ServerB` | 绿（—无观看记录） | 无 FAIL 行 |
| 3 | `ServerF ~(∠・ω<)⌒☆` | **红**（91 天前看过） | `RAIL-PROBE FAIL server=ServerF～(∠・ω< )⌒☆ ShellHttpException` |
| 4 | `步兵营地` | 绿（48 天前看过） | 无 FAIL 行 |
| 5 | `ServerC` | 绿（—无观看记录） | 无 FAIL 行 |
| 6 | `ServerD` | **红**（66 天前看过） | `RAIL-PROBE FAIL server=ServerD` |
| 7 | `ServerD` | **红**（64 天前看过） | `RAIL-PROBE FAIL server=ServerD`（同名两台） |
| 8 | `STAR CORE` | 绿（被视口裁切） | 无 FAIL 行 |

⇒ 验收要求的"**同一窗口内**：真实不可达台 = 红图标 + 红描边，且 `ServerA` = 绿图标"成立（第 3/6/7 行红、第 1 行绿，同一次采样、同一张 PNG）。

### 4.5 门禁读数（一条）

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell\tools\evidence-hygiene-check.ps1
```
`EXIT=0`；`SELF-CHECK|checks=10|pass=10|fail=0|inconclusive=0|at=2026-09-12 08:04:34.289`；`SUMMARY|verdict=PASS|checks=10|pass=10|fail=0|inconclusive=0|scannedFiles=213|citedNames=169`（HEAD `1b7764a`）。
其中 H6-batch-kill `realHits=0`：本卡驱动脚本只 `Stop-Process -Id <自己 Start-Process 返回的 pid>`，属 H6 note 认可的合规形态；H7 emoji `newAstral=0 / newFe0f=0`（新增注释用 ASCII `[!]`/`[?]` 标记，不用 emoji）。

## 5. 未探测期间显示的语义（诚实记录，非缺陷但需知会）

`_ok` 默认 `false`，而 `ServerRow` 构造函数**不赋值** ⇒ 从首帧到该行探测返回之间，图标显示**离线红**。
这与改前既有的 `_detail = "未探测"` + `NameBrush`/`StatusBrush` 已取 fail 色是**同一个状态**，因此**不是本次新增的闪变**（改前只有图标恒绿，描边/名称本来就已是 fail 色）。
若用户判断"未探测即红"不可接受，正确改法是引入三态（未探测＝中性 / 在线＝绿 / 离线＝红），而**不是**回退成本卡之前的恒绿。

**🔴 Captain 2026-09-12 裁定（本节问号已关闭）：维持现状（未探测 = 离线红），不做三态。**
理由（照裁定原文）= 改前同一状态本来就写着 `_detail = "未探测"` **且**名称与描边已取 fail 色，**只有图标恒绿** ⇒ 首帧红图标**不是本次新增的闪变**，而是把图标对齐到既有语义；引入三态**无用户价值**且会扩大范围 ⇒ 不改。日后若要改，属**另一张卡**。

## 6. 证伪条件（本卡的读数怎样才能被推翻）

- 合并两态缓存成一份 ⇒ 先到的那态永久占位 ⇒ §4.4 会出现"红行却是绿图标"（或反之）。
- 删掉 `Raise(nameof(IconSource))` ⇒ 图标停在首帧状态（全红），而名称/描边仍会随探测变化 ⇒ §4.4 的"同状态"不成立。
- 把资产名改回 `emby-offline-preview.png` ⇒ 输出目录无该文件 ⇒ 图标 `null`、红行只剩红描边 ⇒ §4.4 的 `#B52D29` 精确命中数应掉到 0。
- 若屏上红绿来自别的东西（而非这两个资产）⇒ §4.4 的整列精确命中数不可能恰好等于"每图标 133/137 px × 图标个数 + 裁切余量"。

## 7. 可复算命令

```powershell
# 1) 构建（先确认 Get-Process dotnet 为空）
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui3-search-bin\   # EXIT=0 / 0 个错误

# 2) 现场检查（防止踩到别人的实例）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell\tools\list-instances.ps1 -Samples 3 -IntervalMs 1000

# 3) 起壳（隔离输出 + 隔离数据根；只杀自己那支）
#    环境 SHELL_SINGLEINSTANCE_ALLOW_MULTI=1、AIPLAYER_APPDATA_ROOT=<root>
Start-Process E:\ui3-search-bin\AIPlayer.Shell.exe -PassThru

# 4) 抓同屏对照（PrintWindow 单通道）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell\tools\popup-capture.ps1 `
    -Pid <pid> -Auto -ExpectClassLike '*WinUIDesktopWin32*' -Out <out.png>

# 5) 逐像素复核（可对任意一张左栏截图复算）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
    shell\App\Features\Servers\evidence\t102-rail-icon-probe.ps1 -Png <out.png> -Ymin 60
```

## 8. 物化件身份表（当刻实测，`sha256_12`）

| 文件 | bytes | sha256_12 | 说明 |
| --- | --- | --- | --- |
| `shell/App/Features/Servers/evidence/t102-rail-window.png` | 676,377 | `1C627FC35874` | 验收主证：同屏两向对照（PrintWindow，1440×759） |
| `shell/App/Features/Servers/evidence/t102-rail-window.txt` | 1,329 | `6F85A038DEB0` | 抓取源断言（hwnd/pid/class/rect/flags/焦点门/哈希） |
| `shell/App/Features/Servers/evidence/t102-rail-strip.png` | 31,047 | `787C9754879A` | 左栏原分辨率条带（人眼对照） |
| `shell/App/Features/Servers/evidence/t102-rail-probe.txt` | 2,182 | `4DCFF6183136` | 逐像素探针输出（§4.4 的原始行） |
| `shell/App/Features/Servers/evidence/t102-app-log.txt` | 48,566 | `AAF3B87707DB` | 应用自己的日志（APPDATA-ROOT + 7 行 RAIL-PROBE FAIL） |
| `shell/App/Features/Servers/evidence/t102-pre-instances.txt` | 1,906 | `31FCAC2E4F68` | 起壳前现场（allFree=True / spanSec=2.22） |
| `shell/App/Features/Servers/evidence/t102-run.ps1` | 3,299 | `748A35589FAC` | 本次驱动脚本（只杀自己 Start-Process 返回的 pid） |
| `shell/App/Features/Servers/evidence/t102-rail-icon-probe.ps1` | 8,635 | `919F76FBFE8B` | 逐像素探针工具（ASCII-only，只读 PNG） |
| `shell/App/Features/Servers/evidence/t102-emby-offline-6x.png` | 13,504 | `1410D3C8DCBA` | 6 倍放大预览（人眼判色，已移出 `Assets\` 拷贝面） |
| `shell/App/Assets/Icons/emby-offline.png` | 2,012 | `0FDDB4448041` | 离线红图标（本体） |
| `shell/App/Assets/Icons/emby.png` | 2,094 | `3B716E0B523B` | 在线绿图标（本体，未改动） |
| `shell/App/MainWindow.xaml.cs` | 37,774 | `470E484049F9` | 改动集所在源文件（806 行） |

## 9. 工具自身的一次真实缺陷（留痕，防后人踩）

逐像素探针第一版把报告行用 `Write-Output` 发出，而 `Write-Output` 在函数内**也会进入返回值** ⇒ `CLUSTER|` 行被吞进返回数组、`PITCH` 行又把字符串当对象读 `.Y0/.Y1`（打印成一串 `0`），并在 `$list` 只剩一个"空数组元素"时崩成
`Method invocation failed because [System.Object[]] does not contain a method named 'op_Division'`（`return ,$merged` 把数组包成单元素，`@()` 再包一层）。
修法：报告行走脚本级列表统一在末尾打印 + 函数 `return $merged`（不加逗号）。本条的 §4.4 读数**全部出自修复后的版本**。

## 10. 入库与"最后一公里"

- 提交：`99f42e0`（`12 files changed, 915 insertions(+), 5 deletions(-)`）。两个资产改名都被 git 识别为 **R100**（100% 相似 = 逐字节不变），队友当刻暂存的 6 项**原样保留**（本次用显式 pathspec 提交，未误提他人文件）。
- 形态身份（工具 `shell/App/Features/Servers/evidence/id-forms.ps1`，采样 2026-09-12 08:06:28.401 @HEAD `99f42e0`）：
  - `shell/App/MainWindow.xaml.cs` —— worktree = blob = **37,774 B / `470E484049F9`**（CR=0 / 806 行；attr `text=set eol=lf`）⇒ 两形态同件，本账本引用的身份在两种形态下都成立。
  - `shell/App/Assets/Icons/emby-offline.png` —— **2,012 B / `0FDDB4448041`**（attr `text=unset`）⇒ 两形态同件。
- ✅ **用户实际运行的那份 `dist` 已重发布**（Captain 2026-09-12 08:10 执行；旧版备份为同级 `AIPlayer.bak-081053`）。我 08:20:30 @HEAD `365c2ce` 独立复核：
  - dist 的 `Assets\Icons\` 里现在是两枚：离线红图 = **2,012 B / `0FDDB4448041`**（与本卡资产**逐字节相同**）、在线绿图 = **2,094 B / `3B716E0B523B`**；**旧名无残留**，且 dist 全树**没有任何 `evidence` 目录** ⇒ 排除规则在发布面上也成立。
  - 发布主程序 = **1,596,416 B / `950C291CD711` / mtime 2026-09-12 08:10:00**。
  - **出版二进制确实含本次改动**（在该 dll 上做三针扫描，含正负控）：本卡新引入的字面量 `emby-offline.png` ⇒ `utf16even=1` **命中**；旧字面量 `emby-offline-preview.png` ⇒ 三扫（ascii / utf16even / utf16odd）**全 0**（负控成立）；既有串 `RAIL-ICON-LOAD-FAIL` ⇒ `utf16even=1`（正控成立）。
  - ⇒ **用户下次打开就能看到红/绿两态**（与本卡在隔离目录的读数一致）；本卡验收仍以 §4 的隔离输出目录为准，dist 只是**发布面复核**。
- 自查留痕（我自己仪器的假阳性）：我先用 `-Filter '*6x*'` 扫 dist，命中了一个 `…Microsoft.UI.Xaml\Assets\NoiseAsset_256x256_PNG.png`（`256x256` 里的 `6x`）⇒ **不是本卡的 `-6x`**；按精确名复判后确认 `-6x` **不在** dist。教训：模糊通配当判据，**必须先人眼核命中项**再下结论。
- 过期引用（非我领地，仅登记，不改）：`shell/docs/WORKSPACE.md:4251`（Captain 账本）与 `shell/Tests/evidence/dist-cleanliness-review.txt:17-18`、`:155-156`（reviewer 历史读数）仍写旧文件名。历史件不该改写，但**下一次 dist 清洁度复核**会发现文件名集合变了（旧两名字消失、新名出现）。
